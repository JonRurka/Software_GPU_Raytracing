using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Palmmedia.ReportGenerator.Core;
using UnityEngine;

public class RayTracer : MonoBehaviour
{

    public static RayTracer Instance{ get; private set;}

    public ComputeShader trc_shader;
    public RenderTexture renderTexturel;

    public int SamplesPerPixel;
    public int MaxDepth;
    public int Initial_Buffer_Size = 10;
    public bool debug_print = false;
    private int cur_buffer_size;

    public Texture2D test_tex;

    private int trc_k_id = 0;
    private ComputeBuffer RandStateBuffer;
    private ComputeBuffer ObjectBuffer;
    private ComputeBuffer PolyBuffer;
    private ComputeBuffer MaterialBuffer;
    private ComputeBuffer DebugBuffer;
    private ComputeBuffer BvhBuffer;

    public enum MaterialType : int
    {
        Lambertian = 1,
        Metal = 2,
        Dielectric = 3
    }

    public enum HittableType : int
    {
        Sphere = 1,
        Quad = 2,
        Poly = 3
    }

    interface BVHable
    {
        TAABB BoundingBox();
        int Index();
    }

    class TBVH<T> where T : BVHable
    {
        public struct BVH_Node
        {
            public int Leaf;
            public int enabled;
            public TAABB aabb;

            public static int SizeOf()
            {
                return sizeof(int) * 2 + TAABB.SizeOf();
            }
        }

        public TBVH<T> Left;
        public TBVH<T> Right;
        public TAABB Bbox;
        public T Leaf;
        public int Size;

        public TBVH(T[] objects) : this(objects, 0, objects.Length)
        {
        }

        public TBVH(T[] objects, int start, int end)
        {
            Leaf = default(T);

            int axis = Random.Range(0, 2);
            IComparer<T> comparator = 
                  (axis == 0) ? new box_x_compare()
                : (axis == 1) ? new box_y_compare()
                : new box_z_compare();

            int object_span = end - start;
            //Size = object_span;
            
            if (object_span == 1)
            {
                Left = Right = null;
                Leaf = objects[start];
            }
            else if (object_span == 2)
            {
                Left = new TBVH<T>(objects[start]);
                Right = new TBVH<T>(objects[start + 1]);
            }
            else
            {
                System.Array.Sort(objects, start, end, comparator);
                int mid = start + object_span / 2;
                Left = new TBVH<T>(objects, start, mid);
                Right = new TBVH<T>(objects, mid, end);
            }

            Bbox = new TAABB(Left.Bbox, Right.Bbox);
        }

        public TBVH(T obj)
        {
            Left = Right = null;
            Leaf = obj;
        }

        public BVH_Node[] GetBuffer()
        {
            List<BVH_Node> res = new List<BVH_Node>();
            res.Capacity = (int)System.Math.Pow(2, Size + 1) - 1;
            //BVH_Node[] res = new BVH_Node[(int)System.Math.Pow(2, Size + 1) - 1];

            Queue<TBVH<T>> queue = new Queue<TBVH<T>>();
            queue.Append(this);

            int level = 0;
            int cur = 0;
            while(queue.Count > 0)
            {
                int size = queue.Count;
                for (int i = 0; i < size; i++)
                {
                    TBVH<T> node = queue.Dequeue();
                    BVH_Node bvh_node = default;
                    bvh_node.enabled = 1;
                    bvh_node.aabb = node.Bbox;
                    bvh_node.Leaf = (node.Leaf != null) ? (node.Leaf.Index()) : -1;
                    res.Add(bvh_node);

                    if (node.Left != null) queue.Append(node.Left);
                    if (node.Right != null) queue.Append(node.Right);
                }
                level++;
            }
            return res.ToArray();
            //return res;
        }

        private class box_x_compare: IComparer<T>
        {
            public int Compare(T a, T b)
            {
                return box_compare(a, b, 0);
            }
        }

        private class box_y_compare: IComparer<T>
        {
            public int Compare(T a, T b)
            {
                return box_compare(a, b, 1);
            }
        }

        private class box_z_compare: IComparer<T>
        {
            public int Compare(T a, T b)
            {
                return box_compare(a, b, 2);
            }
        }

        private static int box_compare(T a, T b, int axis)
        {
            TInterval a_axis_interval = a.BoundingBox().Axis_Interval(axis);
            TInterval b_axis_interval = b.BoundingBox().Axis_Interval(axis);
            //return a_axis_interval.min < b_axis_interval.min;
            return a_axis_interval.min.CompareTo(b_axis_interval.min);
        }
    }

    class TBVH_Poly : BVHable
    {
        public THitPoly polygon;
        public TAABB aabb;

        public TBVH_Poly(THitPoly p, TAABB ab)
        {
            this.polygon = p;
            this.aabb = ab;
        }

        public TAABB BoundingBox()
        {
            return aabb;
        }

        public int Index()
        {
            throw new System.NotImplementedException();
        }
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

        public TInterval(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public TInterval(TInterval a, TInterval b)
        {
            this.min = a.min <= b.min ? a.min : b.min;
            this.max = a.max >= b.max ? a.max : b.max;
        }

        public static int SizeOf()
        {
            return sizeof(float) * 2;
        }
    };

    struct TAABB{
        public TInterval x;
        public TInterval y;
        public TInterval z;

        public TAABB(TAABB box0, TAABB box1)
        {
            x = new TInterval(box0.x, box1.x);
            y = new TInterval(box0.y, box1.y);
            z = new TInterval(box0.z, box1.z);
        }

        public TAABB(Bounds orig)
        {
            x = new TInterval(orig.min.x, orig.max.x);
            y = new TInterval(orig.min.y, orig.max.y);
            z = new TInterval(orig.min.z, orig.max.z);
        }

        public static int SizeOf()
        {
            return TInterval.SizeOf() * 3;
        }

        public TInterval Axis_Interval(int n) {
            if (n == 1) return y;
            if (n == 2) return z;
            return x;
        }
    };

    struct TMaterial{
        public int material_type;
        public int does_emit;
        public Vector3 albedo;
        public float fuzz;
        public float refraction_index;

        public static int SizeOf()
        {
            return sizeof(int) + sizeof(int) + sizeof(float) * 3 + sizeof(float) * 2;
        }
    };

    /*struct THittable{
        public int hittable_type;
        public int enabled;
        public TRay center;
        public int mat;
        public TAABB bbox;

        public float radius; //sphere

        // Quad
        public Vector3 Q;
        public Vector3 u, v;
        public Vector3 w;
        public Vector3 normal;
        public float D;

        public static int SizeOf()
        {
            return  sizeof(int) + sizeof(int) + TRay.SizeOf() + sizeof(int) + TAABB.SizeOf() + 
                    sizeof(float) +
                    sizeof(float) * 3 * 5 + sizeof(float);
        }
    };*/

    struct THittObject : BVHable
    {
        public int hittable_type;
        public int enabled;
        public int poly_start_idx;
        public int poly_num;
        public Matrix4x4 rotation;
        public TRay center;
        public int mat;
        public TAABB bbox;
        public Vector4 attr1;

    
        public static int SizeOf()
        {
            return sizeof(int) * 5 +
                    sizeof(float) * 4 * 4 +
                   TRay.SizeOf() + TAABB.SizeOf() + 
                   sizeof(float) * 4;
        }

        public TAABB BoundingBox()
        {
            return bbox;
        }

        public int Index()
        {
            throw new System.NotImplementedException();
        }
    }

    struct THitPoly{
        public int hit_obj_idx;
        public Vector4 attr1;
        public Vector4 attr2;
        public Vector4 attr3;

        public static int SizeOf()
        {
            return sizeof(int) + sizeof(float) * 4 * 3;
        }
    };


    class SceneObject : BVHable
    {
        public int ID;
        public int Index;
        public bool Enabled;
        public RayTracedObject.ObjectInfo Obj_Info;
        public RayTracedObject.MaterialInfo Mat_Info;

        public int Poly_Offset;

        public TMaterial get_TMat()
        {
            TMaterial res = new TMaterial();
            res.material_type = (int)Mat_Info.Material_Type;
            res.does_emit = Mat_Info.Emits ? 1 : 0;
            res.albedo = Mat_Info.Albedo;
            res.fuzz = Mat_Info.Fuzz;
            res.refraction_index = Mat_Info.Refraction_Index;
            return res;
        }

        /*public THittObject get_THitt()
        {
            THittable res = new THittable();
            res.hittable_type = (int)Obj_Info.Object_Type;
            res.enabled = Enabled ? 1 : 0;
            res.center = new TRay(Obj_Info.Center);
            res.radius = Obj_Info.Radius;
            res.mat = Index;

            res.Q = Obj_Info.Q;
            res.u = Obj_Info.u;
            res.v = Obj_Info.v;

            Vector3 n = Vector3.Cross(res.u, res.v);
            res.normal = n.normalized;
            res.D = Vector3.Dot(res.normal, res.Q);
            res.w = n / Vector3.Dot(n, n);

            return res;
        }*/

        public THittObject get_THitt()
        {
            THittObject res = new THittObject();
            res.hittable_type = (int)Obj_Info.Object_Type;
            res.enabled = Enabled ? 1 : 0;
            res.poly_start_idx = Poly_Offset;
            res.poly_num = Obj_Info.Polygons.Count;
            res.center = new TRay(Obj_Info.Center);
            res.mat = Index;
            res.bbox = BoundingBox();
            res.rotation = Obj_Info.Rotation;

            return res;

        }

        public THitPoly[] Get_TPoly()
        {
            List<THitPoly> res = new List<THitPoly>();
            res.Capacity = Obj_Info.Polygons.Count;

            foreach (var p in Obj_Info.Polygons)
            {
                THitPoly rp = new THitPoly();
                rp.hit_obj_idx = Index;
                rp.attr1 = p.V0;
                rp.attr2 = p.V1;
                rp.attr3 = p.V2;
                res.Add(rp);
            }

            return res.ToArray();
        }

        public TAABB BoundingBox()
        {
            return new TAABB(Obj_Info.Bounds);
        }

        int BVHable.Index()
        {
            return Index;
        }
    }
    int cur_id = 0;
    Dictionary<int, SceneObject> SceneObjectsMap;
    THittObject[] hittables_buffer;
    THitPoly[] hittable_poly_buffer;
    TMaterial[] materials_buffer;
    SceneObject[] sceneObjects_arr;
    TBVH<SceneObject> BVH;

    THitPoly[] hittable_poly_bvh_process_buffer;

    float timer = 1.0f;
    bool should_sync_poly = false;

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

        if (obj_info.Object_Type == HittableType.Poly)
        {
            should_sync_poly = true;
        }

        return obj.ID;
    }

    public void RemoveObject(int id)
    {
        SceneObject obj = SceneObjectsMap[id];
        SceneObjectsMap.Remove(id);
        hittables_buffer[obj.Index].enabled = 0;

        if (obj.Obj_Info.Object_Type == HittableType.Poly)
        {
            should_sync_poly = true;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        Instance = this;

        cur_buffer_size = Initial_Buffer_Size;
        SceneObjectsMap = new Dictionary<int, SceneObject>();
        hittables_buffer = new THittObject[Initial_Buffer_Size];
        materials_buffer = new TMaterial[Initial_Buffer_Size];
        ObjectBuffer = new ComputeBuffer(Initial_Buffer_Size, THittObject.SizeOf());
        MaterialBuffer = new ComputeBuffer(Initial_Buffer_Size, TMaterial.SizeOf());
        BvhBuffer = new ComputeBuffer((int)System.Math.Pow(2, Initial_Buffer_Size + 1) - 1, TBVH<SceneObject>.BVH_Node.SizeOf());

        for (int i = 0; i < Initial_Buffer_Size; i++)
        {
            hittables_buffer[i].hittable_type = 0;
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

        trc_shader.SetTexture(trc_k_id, "SampleTex", test_tex);
        trc_shader.SetInt("SampleTexWidth", test_tex.width);
        trc_shader.SetInt("SampleTexHeight", test_tex.height);

        int total_size = renderTexturel.width * renderTexturel.height;

        //RandStateBuffer = new ComputeBuffer(total_size, sizeof(float));
        
        DebugBuffer = new ComputeBuffer(total_size, sizeof(float) * 4);

        //trc_shader.SetBuffer(trc_k_id, "rand_state", RandStateBuffer);
        trc_shader.SetBuffer(trc_k_id, "Objects", ObjectBuffer);
        trc_shader.SetBuffer(trc_k_id, "Materials", MaterialBuffer);
        trc_shader.SetBuffer(trc_k_id, "BVH", BvhBuffer);
        trc_shader.SetBuffer(trc_k_id, "debug", DebugBuffer);

        
        

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

    bool did_shoot = false;
    // Update is called once per frame
    void Update()
    {
        if (should_sync_poly)
        {
            should_sync_poly = false;
            sync_polygons();
        }

        timer -= Time.deltaTime;
        if (timer <= 0 && !did_shoot){
            Debug.LogFormat("Start render");
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();  

            sync_objects();
            render();
            did_shoot = true;

            watch.Stop();
            Debug.LogFormat("Render: {0} ms", watch.Elapsed.TotalMilliseconds);
        }
    }

    void sync_polygons()
    {
        List<THitPoly> all_polygons = new List<THitPoly>();
        all_polygons.Capacity = 1000;

        sceneObjects_arr = new SceneObject[SceneObjectsMap.Count];

        int cur_idx = 0;
        int i = 0;
        foreach (KeyValuePair<int, SceneObject> entry in SceneObjectsMap)
        {
            THitPoly[] polygons = entry.Value.Get_TPoly();
            all_polygons.AddRange(polygons);
            entry.Value.Poly_Offset = cur_idx;
            cur_idx += polygons.Length;
            SceneObjectsMap[i++] = entry.Value;
        }

        hittable_poly_buffer = all_polygons.ToArray();
        PolyBuffer = new ComputeBuffer(Mathf.Max(1, hittable_poly_buffer.Length), THitPoly.SizeOf());
        PolyBuffer.SetData(hittable_poly_buffer);
        trc_shader.SetBuffer(trc_k_id, "Polygons", PolyBuffer);
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

        BVH = new TBVH<SceneObject>(sceneObjects_arr);
        var nodes = BVH.GetBuffer();

        BvhBuffer.SetData(nodes);
        ObjectBuffer.SetData(hittables_buffer);
        MaterialBuffer.SetData(materials_buffer);
    }

    void render()
    {
        int seed = Random.Range(System.Int32.MinValue, System.Int32.MaxValue);
        //trc_shader.SetInt("Seed", seed);
        trc_shader.SetFloat("VFOV", 90);
        trc_shader.SetVector("LookFrom", transform.position);
        trc_shader.SetVector("LookAt", transform.position + transform.forward);
        trc_shader.SetVector("VUP", Vector3.up);
        trc_shader.SetFloat("DefocusAngle", 0.0f);
        trc_shader.SetFloat("FocusDist", 5);
        trc_shader.SetInt("NumObjects", cur_buffer_size);

        trc_shader.Dispatch(trc_k_id, renderTexturel.width / 8, renderTexturel.height / 8, 1);

        if (debug_print){
            int total_size = renderTexturel.width * renderTexturel.height;
            Vector4[] debug_vals = new Vector4[total_size];
            DebugBuffer.GetData(debug_vals);

            for(uint i = 0; i < total_size; i++)
            {
                Vector2Int ipos = C_1D_to_2D(i, (uint)renderTexturel.width);
                if (debug_vals[i].x >= 1)
                    Debug.LogFormat("({0}, {1}): {2}", ipos.x, ipos.y, debug_vals[i].ToString());
            }
        }
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
