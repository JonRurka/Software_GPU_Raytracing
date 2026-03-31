using System.Collections.Generic;
using UnityEngine;

public class RayTracer : MonoBehaviour
{

    public static RayTracer Instance{ get; private set;}

    public ComputeShader trc_shader;
    public RenderTexture renderTexturel;

    public int SamplesPerPixel;
    public int MaxDepth;
    public int Initial_Buffer_Size = 10;
    private int cur_buffer_size;

    private int trc_k_id = 0;
    private ComputeBuffer RandStateBuffer;
    private ComputeBuffer ObjectBuffer;
    private ComputeBuffer MaterialBuffer;
    private ComputeBuffer DebugBuffer;

    public enum MaterialType : int
    {
        Lambertian = 1,
        Metal = 2,
        Dielectric = 3
    }

    public enum HittableType : int
    {
        Sphere = 1
    }

    struct TRay{
        public Vector3 orig;
        public Vector3 dir;
        public float tm;

        public TRay(Vector3 static_center)
        {
            orig = static_center;
            dir = Vector3.zero;
            tm = 0;
        }

        public TRay(Vector3 center1, Vector3 center2)
        {
            orig = center1;
            dir = (center2 - center1);
            tm = 0;
        }

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
        public int enabled;
        public TRay center;
        public int mat;
        public TAABB bbox;

        public float radius; //sphere

        public static int SizeOf()
        {
            return sizeof(int) + sizeof(int) + TRay.SizeOf() + sizeof(int) + TAABB.SizeOf() + sizeof(float);
        }
    };

    struct SceneObject
    {
        public int ID;
        public int Index;
        public bool Enabled;
        public RayTracedObject.ObjectInfo Obj_Info;
        public RayTracedObject.MaterialInfo Mat_Info;

        public TMaterial get_TMat()
        {
            TMaterial res = new TMaterial();
            res.material_type = (int)Mat_Info.Material_Type;
            res.albedo = Mat_Info.Albedo;
            res.fuzz = Mat_Info.Fuzz;
            res.refraction_index = Mat_Info.Refraction_Index;
            return res;
        }

        public THittable get_THitt()
        {
            THittable res = new THittable();
            res.hittable_type = (int)Obj_Info.Object_Type;
            res.enabled = Enabled ? 1 : 0;
            res.center = new TRay(Obj_Info.Center);
            res.radius = Obj_Info.Radius;
            res.mat = Index;
            return res;
        }
    }
    int cur_id = 0;
    Dictionary<int, SceneObject> SceneObjectsMap;
    THittable[] hittables_buffer;
    TMaterial[] materials_buffer;

    public int AddObject(RayTracedObject.ObjectInfo obj_info, RayTracedObject.MaterialInfo mat_info)
    {
        SceneObject obj = new SceneObject();
        obj.ID = cur_id++;
        obj.Enabled = true;
        obj.Obj_Info = obj_info;
        obj.Mat_Info = mat_info;
        obj.Index = find_next_index();
        hittables_buffer[obj.Index] = obj.get_THitt();
        materials_buffer[obj.Index] = obj.get_TMat();
        SceneObjectsMap.Add(obj.ID, obj);
        return obj.ID;
    }

    public void RemoveObject(int id)
    {
        SceneObject obj = SceneObjectsMap[id];
        hittables_buffer[obj.Index].enabled = 0;
        SceneObjectsMap.Remove(id);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        Instance = this;

        cur_buffer_size = Initial_Buffer_Size;
        SceneObjectsMap = new Dictionary<int, SceneObject>();
        hittables_buffer = new THittable[Initial_Buffer_Size];
        materials_buffer = new TMaterial[Initial_Buffer_Size];
        ObjectBuffer = new ComputeBuffer(Initial_Buffer_Size, THittable.SizeOf());
        MaterialBuffer = new ComputeBuffer(Initial_Buffer_Size, TMaterial.SizeOf());

        for (int i = 0; i < Initial_Buffer_Size; i++)
        {
            hittables_buffer[i].enabled = 0;
        }

        int seed = 0;// Random.Range(System.Int32.MinValue, System.Int32.MaxValue);

        trc_k_id = trc_shader.FindKernel("CSMain");
        trc_shader.SetTexture(trc_k_id, "Result", renderTexturel);
        trc_shader.SetInt("Width", renderTexturel.width);
        trc_shader.SetInt("Height", renderTexturel.height);
        trc_shader.SetInt("SamplesPerPixel", SamplesPerPixel);
        trc_shader.SetInt("MaxDepth", MaxDepth);
        trc_shader.SetInt("Seed", seed);

        int total_size = renderTexturel.width * renderTexturel.height;

        //RandStateBuffer = new ComputeBuffer(total_size, sizeof(float));
        
        DebugBuffer = new ComputeBuffer(total_size, sizeof(float) * 4);

        //trc_shader.SetBuffer(trc_k_id, "rand_state", RandStateBuffer);
        trc_shader.SetBuffer(trc_k_id, "Objects", ObjectBuffer);
        trc_shader.SetBuffer(trc_k_id, "Materials", MaterialBuffer);
        trc_shader.SetBuffer(trc_k_id, "debug", DebugBuffer);

        

        //TRay o_center_1 = new TRay{orig=new Vector3(0, 1, 2), dir=Vector3.one, tm=0};
        //TRay o_center_2 = new TRay{orig=new Vector3(-1, 1, 2), dir=Vector3.one, tm=0};

        /*TMaterial[] materials =
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
        };*/

        /*THittable[] objects =
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
        };*/

        
        

        //ObjectBuffer.SetData(objects);
        //MaterialBuffer.SetData(materials);
        

        /*Debug.LogFormat("Start render");
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
        Debug.LogFormat("Stop render : {0} ms", watch.Elapsed.TotalMilliseconds);*/
    }

    // Update is called once per frame
    void Update()
    {
        sync_objects();
        render();
    }

    int find_next_index()
    {
        for (int idx = 0; idx < cur_buffer_size; idx++)
        {
            if (hittables_buffer[idx].enabled == 0)
            {
                return idx;
            }
        }

        // TODO: Reallocate.
        return -1;
    }

    void sync_objects()
    {
        foreach (KeyValuePair<int, SceneObject> entry in SceneObjectsMap)
        {
            hittables_buffer[entry.Value.Index] = entry.Value.get_THitt();
            materials_buffer[entry.Value.Index] = entry.Value.get_TMat();
        }
        ObjectBuffer.SetData(hittables_buffer);
        MaterialBuffer.SetData(materials_buffer);
    }

    void render()
    {
        int seed = Random.Range(System.Int32.MinValue, System.Int32.MaxValue);
        trc_shader.SetInt("Seed", seed);
        trc_shader.SetFloat("VFOV", 90);
        trc_shader.SetVector("LookFrom", transform.position);
        trc_shader.SetVector("LookAt", transform.position + transform.forward);
        trc_shader.SetVector("VUP", Vector3.up);
        trc_shader.SetFloat("DefocusAngle", 0.0f);
        trc_shader.SetFloat("FocusDist", 5);
        trc_shader.SetInt("NumObjects", cur_buffer_size);

        trc_shader.Dispatch(trc_k_id, renderTexturel.width / 8, renderTexturel.height / 8, 1);
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
