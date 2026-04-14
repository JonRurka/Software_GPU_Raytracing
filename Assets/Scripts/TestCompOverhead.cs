using UnityEditor.Rendering;
using UnityEngine;

public class TestCompOverhead : MonoBehaviour
{
    public ComputeShader test_shader;
    public int NumLoopExecutions;
    public int loopIter;
    public int Width;
    public int Height;

    private int serpate_k_id = 0;
    private int combine_k_id = 0;

    ComputeBuffer data_buffer;
    System.Diagnostics.Stopwatch watch;

    public float timer = 3.0f;
    bool did_test = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.LogFormat("Init overhead test...");

        serpate_k_id = test_shader.FindKernel("SeperateLoops");
        combine_k_id = test_shader.FindKernel("CombinedLoops");

        data_buffer = new ComputeBuffer(Width * Height, sizeof(float));

        int seed = 0;// Random.Range(0, 1000000);

        test_shader.SetInt("Width", Width);
        test_shader.SetInt("Height", Height);
        test_shader.SetInt("Seed", seed);
        test_shader.SetInt("NumLoops", NumLoopExecutions);
        test_shader.SetInt("loopIter", loopIter);

        test_shader.SetBuffer(serpate_k_id, "Data", data_buffer);
        test_shader.SetBuffer(combine_k_id, "Data", data_buffer);
    }

    // Update is called once per frame
    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0 && !did_test)
        {
            do_test();
            timer = 3.0f;
            //did_test = true;
        }
    }

    void do_test()
    {
        Debug.LogFormat("Executing overhead test...");

        

        watch = new System.Diagnostics.Stopwatch();

        // Reset data
        float[] data = new float[Width * Height];
        data_buffer.SetData(data);

        watch.Restart();
        float seperate_res = test_seperate();
        double seperate_elapsed = watch.Elapsed.TotalMilliseconds;

        // Reset data
        data_buffer.SetData(data);

        watch.Restart();
        float combined_res = test_combined();
        double combined_elapsed = watch.Elapsed.TotalMilliseconds;



        double diff = seperate_elapsed - combined_elapsed;

        Debug.LogFormat("Combined: ({0}, {1} ms), Seperate: ({2}, {3} ms): diff: ({4}ms, {5}ms / iter)", 
            combined_res, combined_elapsed, seperate_res, seperate_elapsed,
            diff, diff / NumLoopExecutions);
    }

    float test_combined()
    {
        
        test_shader.Dispatch(combine_k_id, Width / 8, Height / 8, 1);
        return extract();
    }

    float test_seperate()
    {
        
        for (int i = 0; i < NumLoopExecutions; i++)
        {
            test_shader.Dispatch(serpate_k_id, Width / 8, Height / 8, 1);
        }
        return extract();
    }

    float extract()
    {
        float res = 0;
        float[] data = new float[Width * Height];
        data_buffer.GetData(data);

        watch.Stop();

        for (int i = 0; i < data.Length; i++)
        {
            res += data[i];
        }

        
        return res;
    }
}
