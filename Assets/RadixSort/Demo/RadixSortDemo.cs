using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;
using System;
using System.Linq;
using System.Diagnostics;
using System.IO;

public class RadixSortDemo : MonoBehaviour
{
    [SerializeField] ComputeShader radixSortShader;
    List<SortingResult> sortingResults = new List<SortingResult>();

    bool compareWithOrderBy = false;
    bool compareWithArraySort = false;
    bool compareWithRandomSort = false;
    bool fullSort = false;
    bool saveResultIntoCSV = false;

    int sortingCount = 1000000;
    int iterations = 1;

    enum SortingState
    {
        None,
        IsSorting,
        Done
    }

    private void OnGUI()
    {
        int height = 0;
        GUI.Label(new Rect(10, height += 20, 100, 20), "Radix GPU Sort", EditorStyles.boldLabel);
        compareWithOrderBy = GUI.Toggle(new Rect(10, height += 20, 300, 20), compareWithOrderBy, "Compare with List.OrderBy");
        compareWithArraySort = GUI.Toggle(new Rect(10, height += 20, 300, 20), compareWithArraySort, "Compare with Array.Sort");
        //compareWithRandomSort = GUI.Toggle(new Rect(10, height += 20, 300, 20), compareWithRandomSort, "Compare with Random Sort");
        //fullSort = GUI.Toggle(new Rect(10, height += 20, 300, 20), fullSort, "Perform Full Sort");
        //saveResultIntoCSV = GUI.Toggle(new Rect(10, height += 20, 300, 20), saveResultIntoCSV, "Save Result Into CSV");

        string sortingCountFieldData = GUI.TextField(new Rect(10, height += 20, 100, 20), sortingCount.ToString());
        sortingCountFieldData = Regex.Replace(sortingCountFieldData, "[^.0-9]", "");
        int.TryParse(sortingCountFieldData, out sortingCount);

        string iterationCountFieldData = GUI.TextField(new Rect(10, height += 20, 100, 20), iterations.ToString());
        iterationCountFieldData = Regex.Replace(iterationCountFieldData, "[^.0-9]", "");
        int.TryParse(iterationCountFieldData, out iterations);

        if (GUI.Button(new Rect(10, height += 20, 100, 20), "Sort"))
        {
            List<Func<float[], SortingResult>> sortsToExecute = new List<Func<float[], SortingResult>>();
            sortsToExecute.Add(PerformRadixSort);

            if (compareWithOrderBy)
                sortsToExecute.Add(PerformOrderBy);

            if (compareWithArraySort)
                sortsToExecute.Add(PerformArraySort);

            if (compareWithRandomSort)
                sortsToExecute.Add(PerformSomeRandomSort);

            float[] sortInput = GenerateRandomValues(sortingCount);

            sortingResults = new List<SortingResult>();
            
            //for(int i = 1000; i <= sortingCount; i += 1000)
            //{
            //    float[] randomValues = GenerateRandomValues(i);
            //    for (int j = 0; j < sortsToExecute.Count; j++)
            //    {
            //        List<SortingResult> allResultsSingleSort = new List<SortingResult>();

            //        for (int k = 0; k < iterations; k++)
            //            allResultsSingleSort.Add(sortsToExecute[j](randomValues));

            //        sortingResults.Add(GetSummedResult(allResultsSingleSort));
            //    }

            //    if(i == 1000)
            //        WriteCSVRow("Iterations," + string.Join(",", sortingResults.Select(x => x.sortingName).ToArray()));

            //    WriteCSVRow(i + "," + string.Join(",", sortingResults.Select(x => x.averageTimeTaken).ToArray()));

            //    sortingResults.Clear();
            //}

            for (int i = 0; i < sortsToExecute.Count; i++)
            {
                List<SortingResult> allResultsSingleSort = new List<SortingResult>();

                if (!fullSort)
                {
                    for (int j = 0; j < iterations; j++)
                        allResultsSingleSort.Add(sortsToExecute[i](GenerateRandomValues(sortingCount)));
                }
                else
                {
                    for (int j = 1; j <= sortingCount; j++)
                        allResultsSingleSort.Add(sortsToExecute[i](GenerateRandomValues(j)));
                }

                SortingResult summedResult = new SortingResult();
                summedResult.averageTimeTaken = allResultsSingleSort.Sum(x => x.averageTimeTaken) / allResultsSingleSort.Count();
                summedResult.isCorrectlySorted = allResultsSingleSort.TrueForAll(x => x.isCorrectlySorted);
                summedResult.sortingName = allResultsSingleSort[0].sortingName;
                sortingResults.Add(summedResult);
            }

        }

        for (int i = 0; i < sortingResults.Count; i++)
        {
            GUI.Label(new Rect(10, height += 20, 300, 20), string.Format("--- {0} ---", sortingResults[i].sortingName));
            GUI.Label(new Rect(10, height += 20, 300, 20), "Sort Time: " + sortingResults[i].averageTimeTaken);
            GUI.Label(new Rect(10, height += 20, 300, 20), "Is sorted correctly: " + sortingResults[i].isCorrectlySorted);
        }
    }

    SortingResult GetSummedResult(List<SortingResult> results)
    {
        SortingResult summedResult = new SortingResult();
        summedResult.averageTimeTaken = results.Sum(x => x.averageTimeTaken) / results.Count();
        summedResult.isCorrectlySorted = results.TrueForAll(x => x.isCorrectlySorted);
        summedResult.sortingName = results[0].sortingName;
        return summedResult;
    }

    float[] GenerateRandomValues(int count)
    {
        float[] sortInput = new float[count];
        for (int i = 0; i < sortInput.Length; i++)
            sortInput[i] = UnityEngine.Random.Range(-999f, 999f);
            //sortInput[i] = UnityEngine.Random.Range(0, 2);
            //sortInput[i] = 1;

        return sortInput;
    }

    void WriteCSVRow(params string[] rowValues)
    {
        File.AppendAllText(Path.Combine(Application.dataPath, "PerformanceResults"), string.Join(",", rowValues) + "\n");
    }

    SortingResult PerformOrderBy(float[] input)
    {
        List<float> inputAsList = input.ToList();

        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        IOrderedEnumerable<float> orderedEnumerable = inputAsList.OrderBy(x => x);
        orderedEnumerable.First();

        return new SortingResult()
        {
            sortingName = "List.OrderBy(x => x)",
            averageTimeTaken = stopWatch.ElapsedMilliseconds,
            isCorrectlySorted = true
        };
    }

    SortingResult PerformSomeRandomSort(float[] input)
    {
        List<float> inputAsList = input.ToList();

        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        float temp;

        // traverse 0 to array length 
        for (int i = 0; i < input.Length - 1; i++)

            // traverse i+1 to array length 
            for (int j = i + 1; j < input.Length; j++)

                // compare array element with 
                // all next element 
                if (input[i] < input[j])
                {
                    temp = input[i];
                    input[i] = input[j];
                    input[j] = temp;
                }




        return new SortingResult()
        {
            sortingName = "Some Random Sort",
            averageTimeTaken = stopWatch.ElapsedMilliseconds,
            isCorrectlySorted = true
        };
    }

    RadixSort sort;

    SortingResult PerformRadixSort(float[] input)
    {
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        if(sort == null)
        {
            sort = new RadixSort();
            sort.SetupBuffers(input.Length, radixSortShader);
        }


        sort.Sort(input);
        sort.GetData(input);

        stopWatch.Stop();

        bool isSorted = RadixDemoUtilities.SortingCheck(input);


        return new SortingResult()
        {
            sortingName = "Radix Sort",
            averageTimeTaken = stopWatch.ElapsedMilliseconds,
            isCorrectlySorted = isSorted,
            message = isSorted ? "" : "Wrongly sorted at: " + input.Length
        };
    }

    SortingResult PerformArraySort(float[] input)
    {
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        Array.Sort(input);

        return new SortingResult()
        {
            sortingName = "Array Sort",
            averageTimeTaken = stopWatch.ElapsedMilliseconds,
            isCorrectlySorted = true
        };
    }

    public class SortingResult
    {
        List<float> results = new List<float>();
        public string sortingName;
        public float averageTimeTaken;
        public bool isCorrectlySorted;
        public string message = "";

        private void AddResult(float result)
        {
            results.Add(result);
        }
    }

    void PrintArray<T>(T[] input)
    {
        for (int j = 0; j < input.Length; j++)
            UnityEngine.Debug.Log(j + ", " + input[j]);
    }

    private void OnDisable()
    {
        if (sort != null)
            sort.Dispose();
    }
}
