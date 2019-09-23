using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RadixDemoUtilities
{
    public static bool SortingCheck(float[] sortedInput)
    {
        for (int i = 1; i < sortedInput.Length; i++)
            if (sortedInput[i - 1] > sortedInput[i])
            {
                UnityEngine.Debug.LogError("Not in order: " + sortedInput.Length);
                //PrintArray(sortedInput);

                return false;
            }


        bool isAllZeroes = true;
        for (int i = 0; i < 50; i++)
            if (sortedInput[UnityEngine.Random.Range(0, sortedInput.Length)] != 0)
            {
                isAllZeroes = false;
                break;
            }

        if (isAllZeroes)
        {
            UnityEngine.Debug.LogError("All Zeroes");
            return false;
        }

        return true;
    }

    public static bool SortingCheck(int[] sortedInput)
    {
        for (int i = 1; i < sortedInput.Length; i++)
            if (sortedInput[i - 1] > sortedInput[i])
            {
                UnityEngine.Debug.LogError("Not in order: " + sortedInput.Length);
                //PrintArray(sortedInput);

                return false;
            }


        bool isAllZeroes = true;
        for (int i = 0; i < 50; i++)
            if (sortedInput[UnityEngine.Random.Range(0, sortedInput.Length)] != 0)
            {
                isAllZeroes = false;
                break;
            }

        if (isAllZeroes)
        {
            UnityEngine.Debug.LogError("All Zeroes");
            return false;
        }

        return true;
    }
}
