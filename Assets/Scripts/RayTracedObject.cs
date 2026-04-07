using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

public class RayTracedObject : MonoBehaviour
{
    public RayTracer.HittableType object_type;
    public RayTracer.MaterialType material_type;
    //public Color Albedo;
    public bool Emit;
    public float Fuzz;
    public float Refraction_Index;

    public struct Polygon
    {
        public Vector3 V0;
        public Vector3 V1;
        public Vector3 V2;

        public Polygon(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            this.V0 = v0;
            this.V1 = v1;
            this.V2 = v2;
        }
    }

    public class ObjectInfo
    {
        public RayTracer.HittableType Object_Type;
        public float Radius {get; internal set;}

        public Vector3 Center {get; internal set;}
        public Matrix4x4 Rotation {get; internal set;}

        public Vector3 Q {get; internal set;}
        public Vector3 u {get; internal set;}
        public Vector3 v {get; internal set;}

        public List<Polygon> Polygons {get; internal set;}
    }

    public class MaterialInfo
    {
        public RayTracer.MaterialType Material_Type;
        public bool Emits{get; internal set;}
        public Vector3 Albedo {get; internal set;}
        public float Fuzz {get; internal set;}
        public float Refraction_Index {get; internal set;}
    }

    
    public ObjectInfo Object_Info {get; private set;}
    public MaterialInfo Material_Info {get; private set;}

    private int ID;
    Material material;
    bool polygons_loaded = false;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        material = GetComponent<MeshRenderer>().material;
        Object_Info = new ObjectInfo();
        Material_Info = new MaterialInfo();
        update_stats();
        ID = RayTracer.Instance.AddObject(Object_Info, Material_Info);
    }

    void OnDestroy()
    {
        RayTracer.Instance.RemoveObject(ID);
    }

    // Update is called once per frame
    void Update()
    {
        update_stats();
    }

    void update_stats()
    {
        Color albedo = material.color;

        Object_Info.Object_Type = object_type;
        



        switch (object_type)
        {
            case RayTracer.HittableType.Sphere:
                Object_Info.Center = transform.position;
                Object_Info.Radius = transform.lossyScale.x * 0.5f;
                break;

            case RayTracer.HittableType.Quad:
                Vector3 right = transform.right;
                Vector3 up = transform.up;
                Vector3 pos = transform.position;
                Vector3 scale = transform.lossyScale;
                Object_Info.Q = pos - (right * scale.x * 0.5f) - (up * scale.y * 0.5f);
                Object_Info.u = up * scale.y;
                Object_Info.v = right * scale.x;
                break;
            
            case RayTracer.HittableType.Poly:
                load_polygons();
                Object_Info.Center = transform.position;
                Object_Info.Rotation = Matrix4x4.Rotate(transform.rotation) * Matrix4x4.Scale(transform.lossyScale);
                break;
        }

        Material_Info.Material_Type = material_type;
        Material_Info.Emits = Emit;
        Material_Info.Albedo = new Vector3(albedo.r, albedo.g, albedo.b);
        Material_Info.Fuzz = Fuzz;
        Material_Info.Refraction_Index = Refraction_Index;
    }

    void load_polygons()
    {
        if (polygons_loaded)
            return;

        Object_Info.Polygons = new List<Polygon>();

        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] indices = mesh.triangles;

        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 v0 = verts[indices[i + 0]];
            Vector3 v1 = verts[indices[i + 1]];
            Vector3 v2 = verts[indices[i + 2]];

            Polygon p = new Polygon(v0, v1, v2);
            Object_Info.Polygons.Add(p);
        }
        polygons_loaded = true;
    }
}
