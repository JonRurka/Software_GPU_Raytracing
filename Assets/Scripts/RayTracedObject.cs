using System.Runtime.CompilerServices;
using UnityEngine;

public class RayTracedObject : MonoBehaviour
{
    public RayTracer.HittableType object_type;
    public RayTracer.MaterialType material_type;
    //public Color Albedo;
    public float Fuzz;
    public float Refraction_Index;

    public class ObjectInfo
    {
        public RayTracer.HittableType Object_Type;
        public float Radius {get; internal set;}
        public Vector3 Center {get; internal set;}
    }

    public class MaterialInfo
    {
        public RayTracer.MaterialType Material_Type;
        public Vector3 Albedo {get; internal set;}
        public float Fuzz {get; internal set;}
        public float Refraction_Index {get; internal set;}
    }

    
    public ObjectInfo Object_Info {get; private set;}
    public MaterialInfo Material_Info {get; private set;}

    private int ID;
    Material material;


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
        Object_Info.Radius = transform.lossyScale.x * 0.5f;
        Object_Info.Center = transform.position;

        Material_Info.Material_Type = material_type;
        Material_Info.Albedo = new Vector3(albedo.r, albedo.g, albedo.b);
        Material_Info.Fuzz = Fuzz;
        Material_Info.Refraction_Index = Refraction_Index;
    }
}
