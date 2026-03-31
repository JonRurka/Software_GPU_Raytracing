using UnityEngine;

public class RayTracer : MonoBehaviour
{

    public ComputeShader trc_shader;
    public RenderTexture renderTexturel;

    public int SamplesPerPixel;
    public int MaxDepth;

    private int trc_k_id = 0;
    private ComputeBuffer RandStateBuffer;
    private ComputeBuffer ObjectBuffer;
    private ComputeBuffer MaterialBuffer;
    private ComputeBuffer DebugBuffer;

    enum MaterialType : int
    {
        Lambertian = 1,
        Metal = 2,
        Dielectric = 3
    }

    enum HittableType : int
    {
        Sphere = 1
    }

    struct TRay{
        public Vector3 orig;
        public Vector3 dir;
        public float tm;

        public static int SizeOf()
        {
            return sizeof(float) * 3 + sizeof(float) * 3 + sizeof(float);
        }
    };

    struct TInterval{
        public float min;
        public float max;

        public static int SizeOf()
        {
            return sizeof(float) * 2;
        }
    };

    struct TAABB{
        public TInterval x;
        public TInterval y;
        public TInterval z;

        public static int SizeOf()
        {
            return TInterval.SizeOf() * 3;
        }
    };

    struct TMaterial{
        public int material_type;
        public Vector3 albedo;
        public float fuzz;
        public float refraction_index;

        public static int SizeOf()
        {
            return sizeof(int) + sizeof(float) * 3 + sizeof(float) * 2;
        }
    };

    struct THittable{
        public int hittable_type;
        public TRay center;
        public int mat;
        public TAABB bbox;

        public float radius; //sphere

        public static int SizeOf()
        {
            return sizeof(int) + TRay.SizeOf() + sizeof(int) + TAABB.SizeOf() + sizeof(float);
        }
    };

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        int seed = 0;// Random.Range(System.Int32.MinValue, System.Int32.MaxValue);

        trc_k_id = trc_shader.FindKernel("CSMain");
        trc_shader.SetTexture(trc_k_id, "Result", renderTexturel);
        trc_shader.SetInt("Width", renderTexturel.width);
        trc_shader.SetInt("Height", renderTexturel.height);
        trc_shader.SetInt("SamplesPerPixel", SamplesPerPixel);
        trc_shader.SetInt("MaxDepth", MaxDepth);
        trc_shader.SetInt("Seed", seed);

        trc_shader.SetFloat("VFOV", 90);
        trc_shader.SetVector("LookFrom", Vector3.zero);
        trc_shader.SetVector("LookAt", Vector3.forward);
        trc_shader.SetVector("VUP", Vector3.up);
        trc_shader.SetFloat("DefocusAngle", 0.0f);
        trc_shader.SetFloat("FocusDist", 5);


        TRay o_center_1 = new TRay{orig=new Vector3(0, 1, 2), dir=Vector3.one, tm=0};
        TRay o_center_2 = new TRay{orig=new Vector3(-1, 1, 2), dir=Vector3.one, tm=0};

        TMaterial[] materials =
        {
            new TMaterial
            {
                material_type=(int)MaterialType.Metal,
                albedo=new Vector3(0.9f, 0.9f, 0.9f),
                fuzz=0.05f
            },
            new TMaterial
            {
                material_type=(int)MaterialType.Metal,
                albedo=new Vector3(0.9f, 0.9f, 0.9f),
                fuzz=0.06f
            }
        };

        THittable[] objects =
        {
            new THittable{
                hittable_type=(int)HittableType.Sphere,
                center=o_center_1,
                mat=0,
                radius=0.3f
            },
            new THittable{
                hittable_type=(int)HittableType.Sphere,
                center=o_center_2,
                mat=1,
                radius=0.5f
            }
        };

        
        int total_size = renderTexturel.width * renderTexturel.height;

        //RandStateBuffer = new ComputeBuffer(total_size, sizeof(float));
        ObjectBuffer = new ComputeBuffer(objects.Length, THittable.SizeOf());
        MaterialBuffer = new ComputeBuffer(materials.Length, TMaterial.SizeOf());
        DebugBuffer = new ComputeBuffer(total_size, sizeof(float) * 4);

        //trc_shader.SetBuffer(trc_k_id, "rand_state", RandStateBuffer);
        trc_shader.SetBuffer(trc_k_id, "Objects", ObjectBuffer);
        trc_shader.SetBuffer(trc_k_id, "Materials", MaterialBuffer);
        trc_shader.SetBuffer(trc_k_id, "debug", DebugBuffer);

        ObjectBuffer.SetData(objects);
        MaterialBuffer.SetData(materials);
        trc_shader.SetInt("NumObjects", objects.Length);

        Debug.LogFormat("Start render");
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

        trc_shader.Dispatch(trc_k_id, renderTexturel.width / 8, renderTexturel.height / 8, 1);

        Vector4[] debug_vals = new Vector4[total_size];
        DebugBuffer.GetData(debug_vals);

        float[] rand_vals = new float[total_size];
        //RandStateBuffer.GetData(rand_vals);

        for(uint i = 0; i < total_size; i++)
        {
            Vector2Int ipos = C_1D_to_2D(i, (uint)renderTexturel.width);
            if (debug_vals[i].w >= 1)
                Debug.LogFormat("({0}, {1}): {2}", ipos.x, ipos.y, debug_vals[i].ToString());
        }

        watch.Stop();
        Debug.LogFormat("Stop render : {0} ms", watch.Elapsed.TotalMilliseconds);
    }

    // Update is called once per frame
    void Update()
    {
        


    }

    uint C_2D_to_1D(int x, int y, uint width) {
        return (uint)(y * width + x);
    }

    Vector2Int C_1D_to_2D(uint i, uint width) {
        int y = (int)(i / width);
        int x = (int)(i % width);

        return new Vector2Int(x, y);
    }
}
