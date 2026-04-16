#ifndef CORE_LIB
#define CORE_LIB

typedef float3 point3;
typedef float3 vec3;
typedef float4 vec4;
typedef float3 color;
typedef int mat_type;
typedef int mat_ID;
typedef int hit_type;

#define MATERIAL_TYPE_LAMBERTIAN    ((mat_type)1)
#define MATERIAL_TYPE_METAL         ((mat_type)2)
#define MATERIAL_TYPE_DIELECTRIC    ((mat_type)3)

#define HITTABLE_TYPE_SPHERE        ((hit_type)1)
#define HITTABLE_TYPE_QUAD          ((hit_type)2)
#define HITTABLE_TYPE_POLY          ((hit_type)3)

#define POS_INF asfloat(0x7F800000)
#define NEG_INF asfloat(0xFF800000)
#define PI (3.1415926535897932385)

uint C_2D_to_1D(int3 id, uint width) {
    return uint(id.y * width + id.x);
}

int2 C_1D_to_2D(uint i, uint width) {
    int y = int(i / width);
    int x = int(i % width);

    return int2(x, y);
}

float degrees_to_radians(float degrees) {
    return (degrees * PI) / 180.0f;
}

float linear_to_gamma(float linear_comp) {
    if (linear_comp > 0)
        return sqrt(linear_comp);
    return 0;
}

float length_squared(float3 e){
    return e[0]*e[0] + e[1]*e[1] + e[2]*e[2];
}

float length(float3 e){
    return sqrt(length_squared(e));
}

inline vec3 unit_vector(const vec3 v) {
    return v / length(v);
}

bool vec3_near_zero(float3 e) {
    // Return true if the vector is close to zero in all dimensions.
    float s = 1e-8;
    return (abs(e[0]) < s) && (abs(e[1]) < s) && (abs(e[2]) < s);
}

vec3 vec3_reflect(const vec3 v, const vec3 n) {
    return v - 2*dot(v,n)*n;
}

vec3 vec3_refract(const vec3 uv, const vec3 n, float etai_over_etat) {
    float cos_theta = min(dot(-uv, n), 1.0);
    vec3 r_out_perp = etai_over_etat * (uv + cos_theta*n);
    vec3 r_out_parallel = -sqrt(abs(1.0 - length_squared(r_out_perp))) * n;
    return r_out_perp + r_out_parallel;
}

/*struct ray{
    point3 orig;
    vec3 dir;
    float tm;
};*/
struct ray{
    float4 fattr1;
    float4 fattr2;
};

#define ray_orig(r)     (r.fattr1.xyz)
#define ray_dir(r)      (r.fattr2.xyz)
#define ray_tm(r)       (r.fattr1.w)

struct interval{
    float min;
    float max;
};

struct aabb{
    interval x;
    interval y;
    interval z;
};

struct material{
    mat_type material_type;
    int does_emit;
    color albedo;
    float fuzz;
    float refraction_index;
};

/*struct hit_record{
    point3 position;
    vec3 normal;
    mat_ID mat;
    float t;
    float2 uv;
    bool front_face;
};*/

struct hit_record{
    float4 fattr1; // position, t
    float4 fattr2; // uv, mat, front_face
    float4 fattr3; // normal
    //int4 iattr1; // mat, front_face
};

#define hit_record_position(h)  (h.fattr1.xyz)
#define hit_record_t(h)         (h.fattr1.w)
#define hit_record_uv(h)        (h.fattr2.xy)
#define hit_record_normal(h)    (h.fattr3.xyz)

#define hit_record_mat_get(h)       (int(h.fattr2.z))
#define hit_record_mat_set(h, v)    (h.fattr2.z = float(v))

#define hit_record_front_face_get(h)    (int(h.fattr2.w) == 1 ? true : false) 
#define hit_record_front_face_set(h, v) (h.fattr2.w = float(v == true ? 1 : 0)) 

struct hittable{
    hit_type hittable_type;
    int enabled;
    ray center;
    mat_ID mat;
    aabb bbox;

    float radius; //sphere

    point3 Q;
    vec3 u, v;
    vec3 w;
    vec3 normal;
    float D;

    vec3 v0;
    vec3 v1;
    vec3 v2;
};

struct hit_object{
    hit_type hittable_type;
    int enabled;
    int poly_start_idx;
    int poly_num;
    float4x4 rotation;
    ray center;
    mat_ID mat;
    aabb bbox;
    vec4 attr1;
};

struct hit_poly{
    int hit_obj_idx;
    vec4 attr1;
    vec4 attr2;
    vec4 attr3;
};

struct bvh_node
{
    int Leaf;
    int enabled;
    int left;
    int right;
    aabb bounds;
};

struct pixel_orientation{
    vec4 pixel00_loc; 
    vec4 pixel_delta_u; 
    vec4 pixel_delta_v; 
    vec4 defocus_disk_u; 
    vec4 defocus_disk_v;
};

#define BVH_NODE_Leaf(b)     (b.Leaf)
#define BVH_NODE_Enabled(b)  (b.enabled)
#define BVH_NODE_BOUNDS(b)   (b.bounds)

#define HIT_OBJECT_V0(o) (o.attr1.xyz)
#define HIT_OBJECT_V1(o) (o.attr2.xyz)
#define HIT_OBJECT_V2(o) (o.attr3.xyz)

typedef hittable sphere;

ray ray_new(point3 origin, vec3 direction, float t){
    ray r;
    r.fattr1 = float4(0,0,0,0);
    r.fattr2 = float4(0,0,0,0);
    ray_orig(r) = origin;
    ray_dir(r) = direction;
    ray_tm(r) = t;
    //r.orig = origin;
    //r.dir = direction;
    //r.tm = t;
    return r;
}

ray ray_new(point3 origin, vec3 direction){
    return ray_new(origin, direction, 0);
}

ray ray_new(){
    return ray_new(point3(0, 0, 0), vec3(0, 1, 0));
}

point3 ray_at(const ray r, float t){
    return ray_orig(r) + t * ray_dir(r);
}

interval interval_new(float min, float max)
{
    interval res;
    res.min = min;
    res.max = max;
    return res;
}

interval interval_new(){ return interval_new(POS_INF, NEG_INF); }

interval interval_new(interval a, interval b)
{
    // Create the interval tightly enclosing the two input intervals.    
    interval res;
    res.min = a.min <= b.min ? a.min : b.min;
    res.max = a.max >= b.max ? a.max : b.max;
    return res;
}

interval interval_empty(){ return interval_new(); }

interval interval_universe(){ return interval_new(NEG_INF, POS_INF); }

float interval_size(interval i){ return i.max - i.min; }

bool interval_contains(interval i, float x)  { return i.min <= x && x <= i.max; }

bool interval_surrounds(interval i, float x)  { return i.min < x && x < i.max; }

float interval_clamp(interval i, float x) 
{
    if (x < i.min) return i.min;
    if (x > i.max) return i.max;
    return x;
}

interval interval_expand(interval i, float delta) 
{
    float padding = delta / 2;
    return interval_new(i.min - padding, i.max + padding);
}

const interval aabb_axis_interval(aabb b, int n) 
{
    if (n == 1) return b.y;
    if (n == 2) return b.z;
    return b.x;
}

aabb aabb_new(interval x, interval y, interval z)
{
    aabb r;
    r.x = x;
    r.y = y;
    r.z = z;
    return r;
}

bool aabb_hit(aabb b, const ray r, interval ray_t)
{
    const point3 ray_orig = ray_orig(r);
    const vec3 ray_dir = ray_dir(r);

    for (int axis = 0; axis < 3; axis++) {
        const interval ax = aabb_axis_interval(b, axis);
        const float adinv = 1.0 / ray_dir[axis];

        float t0 = (ax.min - ray_orig[axis]) * adinv;
        float t1 = (ax.max - ray_orig[axis]) * adinv;

        if (t0 < t1) {
            if (t0 > ray_t.min) ray_t.min = t0;
            if (t1 < ray_t.max) ray_t.max = t1;
        } else {
            if (t1 > ray_t.min) ray_t.min = t1;
            if (t0 < ray_t.max) ray_t.max = t0;
        }

        if (ray_t.max <= ray_t.min)
                return false;
    }
    return true;
}

hit_record hit_record_new()
{
    hit_record rec;
    rec.fattr1 = float4(0,0,0,0);
    rec.fattr2 = float4(0,0,0,0);
    rec.fattr3 = float4(0,0,0,0);
    //rec.iattr1 = int4(0,0,0,0);

    hit_record_position(rec) = point3(0, 0, 0);
    hit_record_t(rec) = 0;
    hit_record_uv(rec) = float2(0, 0);
    hit_record_normal(rec) = vec3(0, 0, 0);
    hit_record_mat_set(rec, 0);
    hit_record_front_face_set(rec, true);
    //rec.position = point3(0, 0, 0);
    //rec.normal = vec3(0, 1, 0);
    //rec.mat = 0;
    //rec.t = 0;
    //rec.uv = float2(0,0);
    //rec.front_face = true;
    return rec;
}

void hit_record_set_face_normal(inout hit_record h, const ray r, const vec3 outward_normal)
{
    // Sets the hit record normal vector.
    // NOTE: the parameter `outward_normal` is assumed to have unit length.

    hit_record_front_face_set(h, dot(ray_dir(r), outward_normal) < 0);
    hit_record_normal(h) = hit_record_front_face_get(h) ? outward_normal : -outward_normal;
}


#endif