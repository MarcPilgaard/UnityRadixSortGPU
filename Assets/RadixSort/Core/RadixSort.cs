using UnityEngine;

public class RadixSort
{
    const int groupSharedMemorySize = 2048;
    const int groupSharedMemorySizeBase = 11;
    const int levels = 32;
    
    public ComputeBuffer bufferOne;
    public ComputeBuffer bufferTwo;
    public ComputeBuffer prefixSumBuffer;
    public ComputeBuffer largestNumberBuffer;
    public ComputeShader radixSort;

    int IDlevel;
    int IDcomparisonBit;
    int IDbufferOne;
    int IDbufferTwo;
    int IDprefixSum;
    int IDsweepParam;
    int IDfromStride;
    int IDjunkIterations;
    int IDjunkPadding;
    int IDpreviousJunkPadding;

    int identifyBitsKernel;
    int upsweepKernel;
    int downsweepKernel;
    int populateOutputBufferWithZeroesKernel;
    int flipNegativeKernel;
    int setLastIndexToZeroKernel;

    int[] kernelsUsingSwapBuffers;

    public static bool havePrinted = false;
    int currentBufferSize = 0;

    public void SetupBuffers(int count, ComputeShader shader)
    {
        radixSort = shader;

        IDlevel = Shader.PropertyToID("level");
        IDcomparisonBit = Shader.PropertyToID("comparisonBit");
        IDbufferOne = Shader.PropertyToID("bufferOne");
        IDbufferTwo = Shader.PropertyToID("bufferTwo");
        IDprefixSum = Shader.PropertyToID("prefixSum");
        IDsweepParam = Shader.PropertyToID("sweepParam");
        IDfromStride = Shader.PropertyToID("fromStride"); ;
        IDjunkIterations = Shader.PropertyToID("junkIterations"); ;
        IDjunkPadding = Shader.PropertyToID("junkPadding"); ;
        IDpreviousJunkPadding = Shader.PropertyToID("previousJunkPadding");

        identifyBitsKernel = radixSort.FindKernel("IdentifyBits");
        upsweepKernel = radixSort.FindKernel("Upsweep");
        downsweepKernel = radixSort.FindKernel("DownSweep");
        populateOutputBufferWithZeroesKernel = radixSort.FindKernel("PopulateOutputBufferWithZeroes");
        flipNegativeKernel = radixSort.FindKernel("FlipNegatives");
        setLastIndexToZeroKernel = radixSort.FindKernel("SetLastIndexToZero");

        largestNumberBuffer = new ComputeBuffer(2, sizeof(uint));
        radixSort.SetBuffer(downsweepKernel, "largestNumber", largestNumberBuffer);
        radixSort.SetBuffer(populateOutputBufferWithZeroesKernel, "largestNumber", largestNumberBuffer);
        radixSort.SetBuffer(flipNegativeKernel, "largestNumber", largestNumberBuffer);

        kernelsUsingSwapBuffers = new int[]
        {
            identifyBitsKernel,
            populateOutputBufferWithZeroesKernel,
            flipNegativeKernel,
            downsweepKernel
        };
    }

    public void Sort(float[] arrToSort)
    {
        int count = arrToSort.Length;
        
        if (count > currentBufferSize)
            SetCountBuffers(count);

        radixSort.SetBuffer(identifyBitsKernel, IDprefixSum, prefixSumBuffer);
        radixSort.SetBuffer(upsweepKernel, IDprefixSum, prefixSumBuffer);
        radixSort.SetBuffer(downsweepKernel, IDprefixSum, prefixSumBuffer);
        radixSort.SetBuffer(populateOutputBufferWithZeroesKernel, IDprefixSum, prefixSumBuffer);
        radixSort.SetBuffer(flipNegativeKernel, IDprefixSum, prefixSumBuffer);
        radixSort.SetBuffer(setLastIndexToZeroKernel, IDprefixSum, prefixSumBuffer);

        bufferOne.SetData(arrToSort);
        Sort(count);
    }

    private void SetCountBuffers(int count)
    {
        if (bufferOne != null)
            bufferOne.Dispose();

        if (bufferTwo != null)
            bufferTwo.Dispose();

        if (prefixSumBuffer != null)
            prefixSumBuffer.Dispose();

        bufferOne = new ComputeBuffer(count, sizeof(float));
        bufferTwo = new ComputeBuffer(count, sizeof(float));
        prefixSumBuffer = new ComputeBuffer(count, sizeof(uint));

        currentBufferSize = count;
    }

    public void Sort(int count)
    {
        radixSort.SetInt("lastIndex", count - 1);

        int linearThreadGroupSizeIterator = MaxThreadCount(count, 1024);
        
        //Iterate through all the bits in a float
        for (int i = 0; i < levels; i++)
        {
            //We figure out which buffer is our "input" buffer and our "output" buffer
            bool bufferLevel = i % 2 == 0;
            SwapBuffers(kernelsUsingSwapBuffers, bufferLevel, IDbufferOne, IDbufferTwo);

            //We identify whether the last bit is a "0" or a "1"
            radixSort.SetInt(IDlevel, i);
            radixSort.Dispatch(identifyBitsKernel, linearThreadGroupSizeIterator, 1, 1);

            //We calculate the prefix sum
            SweepRecursively(count, 2, 0);

            //We move values from our input buffer to our output buffer based on the calculated information in the prefix sum
            radixSort.Dispatch(populateOutputBufferWithZeroesKernel, linearThreadGroupSizeIterator, 1, 1);
        }
        
        SwapBuffers(kernelsUsingSwapBuffers, true, IDbufferOne, IDbufferTwo);

        //Because float's most significant bit is a sign bit, we need to treat the last bit as a special case
        radixSort.Dispatch(flipNegativeKernel, linearThreadGroupSizeIterator, 1, 1);
    }

    public void GetData(float[] outputArr)
    {
        bufferTwo.GetData(outputArr);
    }

    public void Dispose()
    {
        bufferOne.Dispose();
        bufferTwo.Dispose();
        prefixSumBuffer.Dispose();
        largestNumberBuffer.Dispose();
    }

    void SweepRecursively(int elementsLeft, int fromStride, int previousJunkPadding)
    {
        int junkPadding = GetJunkAmount(elementsLeft);
        int threadGroups = Mathf.CeilToInt((float)elementsLeft / groupSharedMemorySize);

        SetSweepParameter(fromStride, junkPadding / 2, junkPadding, previousJunkPadding);

        //We perform the "Upsweep" step when calculating the prefixsum
        radixSort.Dispatch(upsweepKernel, threadGroups, 1, 1);

        if (threadGroups != 1)
            SweepRecursively(threadGroups, (fromStride << groupSharedMemorySizeBase), junkPadding);
        else
            radixSort.Dispatch(setLastIndexToZeroKernel, 1, 1, 1);

        SetSweepParameter(fromStride, junkPadding / 2, junkPadding, previousJunkPadding);

        //We perform the "Downsweep" calculations when we are done with the "Upsweep" calculation
        radixSort.Dispatch(downsweepKernel, threadGroups, 1, 1);
    }

    void SetSweepParameter(int fromStride, int junkIterations, int junkPadding, int previousJunkPadding)
    {
        radixSort.SetInt(IDfromStride, fromStride);
        radixSort.SetInt(IDjunkIterations, junkIterations);
        radixSort.SetInt(IDjunkPadding, junkPadding);
        radixSort.SetInt(IDpreviousJunkPadding, previousJunkPadding);
    }

    int GetJunkAmount(int count)
    {
        int junkPadding = groupSharedMemorySize - count % groupSharedMemorySize;

        if (junkPadding == groupSharedMemorySize)
            junkPadding = 0;

        return junkPadding;
    }

    void PrintBuffer<T>(ComputeBuffer buffer, int count, string prefix, int printFromIndex = 0)
    {
        T[] outData = new T[count];
        buffer.GetData(outData);

        for (int debugIndex = 0; debugIndex < outData.Length; debugIndex++)
            if(debugIndex >= printFromIndex)
                Debug.Log(prefix + ", " + debugIndex + ", " + outData[debugIndex]);
    }

    void SwapBuffers(int[] kernelsUsingSwap, bool toOriginal, int bufferOneID, int bufferTwoID)
    {
        for (int kernelIdx = 0; kernelIdx < kernelsUsingSwap.Length; kernelIdx++)
        {
            radixSort.SetBuffer(kernelsUsingSwap[kernelIdx], bufferOneID, toOriginal ? bufferOne : bufferTwo);
            radixSort.SetBuffer(kernelsUsingSwap[kernelIdx], bufferTwoID, toOriginal ? bufferTwo : bufferOne);
        }
    }

    int MaxThreadCount(int amountOfEntries, int kernelSize)
    {
        int remainder = amountOfEntries % kernelSize;
        return Mathf.RoundToInt((Mathf.Max(0, amountOfEntries - remainder)) / kernelSize) + (remainder > 0 ? 1 : 0);
    }
}