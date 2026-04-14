#ifndef RAND_LIB
#define RAND_LIB

struct rand_state{
    float3 cur_val;
    int3 id;
};
rand_state rand_state_new(int3 id){
    rand_state res;
    res.cur_val = float3(max(1, uint(id.x + 1 + Seed) % uint(Width)), max(1, uint(id.y + 1 + Seed) % uint(Height)), 0);
    res.id = id;
    return res;
}

//RWStructuredBuffer<float> rand_state;
inline float random_double(inout rand_state state)
{
    float cur_val = frac(sin(dot(float2(state.cur_val.xy), float2(12.9898, 78.233))) * 43758.5453123);
    state.cur_val += float3(max(1, state.id.x + 1 + cur_val), max(1, state.id.y + 1 + cur_val), 0);
    state.cur_val = float3(fmod(state.cur_val.x, Width), fmod(state.cur_val.y, Height), 0);
    return cur_val;
}

inline float random_double(inout rand_state state, float min, float max) {
    return min + (max - min) * random_double(state);
}

inline int random_int(inout rand_state state, int min, int max) {
    // Returns a random integer in [min,max].
    return int(random_double(state, min, max + 1));
}

vec3 random_vec3(inout rand_state state){
    return vec3(random_double(state), random_double(state), random_double(state));
}

vec3 random_vec3(inout rand_state state, float min, float max) {
    return vec3(random_double(state, min, max), random_double(state, min, max), random_double(state, min, max));
}

vec3 random_in_unit_disk(inout rand_state state) {
    int n = 50;
    while (true) {
        if (n <= 0)
            return vec3(POS_INF, POS_INF, POS_INF);
        vec3 p = vec3(random_double(state, -1, 1), random_double(state, -1, 1), 0);
        if (length_squared(p) < 1)
            return p;
        n-=1;
    }
}

vec3 random_unit_vector(inout rand_state state, inout int n, inout vec3 p) {
    //int n = 50;
    while (true) {
        if (n <= 0)
            return vec3(POS_INF, POS_INF, POS_INF);
        p = random_vec3(state, -1, 1);
        float lensq = length_squared(p);
        if (1e-160 < lensq && lensq <= 1)
            return p / sqrt(lensq);
        n-=1;
    }
}

vec3 random_unit_vector(inout rand_state state){
    int n = 50;
    vec3 p;
    return random_unit_vector(state, n, p);
}

vec3 random_on_hemisphere(inout rand_state state, const vec3 normal) {
    vec3 on_unit_sphere = random_unit_vector(state);
    if (dot(on_unit_sphere, normal) > 0.0)
        return on_unit_sphere;
    else
        return -on_unit_sphere;
}

vec3 sample_square(inout rand_state state) {
    // Returns the vector to a random point in the [-.5,-.5]-[+.5,+.5] unit square.
    return vec3(random_double(state) - 0.5, random_double(state) - 0.5, 0);
}

#endif
