#include <cmath>

#define DLLEXPORT extern "C" __declspec(dllexport)

// Keeping track of the spring's state across frames
static float currentSpread = 0.0f;
static float velocity = 0.0f;

// Physical constants
const float mass = 1.0f;
const float springConstant = 150.0f; // k: snappiness
const float dampingCoefficient = 20.0f; // c: damping


DLLEXPORT float UpdateCrosshairPhysics(float speedXZ, float speedY, bool isFiring, bool isADS, float deltaTime) {
    if (deltaTime <= 0.0f) return currentSpread; 
    if (deltaTime > 0.05f) deltaTime = 0.016f; // Clamp to prevent load-screen explosions

    float movementForce = (speedXZ * 6000.0f) + (std::abs(speedY) * 1200.0f);
    if (isADS) {
        movementForce *= .5f; // Arbitrary recoil force when firing
    }

    float externalForce = movementForce;

    if (isFiring) {
        float fireRecoil = 3000.0f;
        // ADS also tightens firing recoil
        if (isADS) fireRecoil *= 0.7f; 
        externalForce += fireRecoil;
    }
    // Hooke's law: F = -kx
    float springForce = -springConstant * currentSpread;
    float dampingForce = -dampingCoefficient * velocity;

    float netForce = springForce + dampingForce + externalForce;
    float acceleration = netForce / mass;

    // Numerical Integration (Euler's method)
    velocity += acceleration * deltaTime;
    currentSpread += velocity * deltaTime;

    if (currentSpread < 0.0f) {
        currentSpread = 0.0f;
        velocity = 0.0f; // Reset velocity when hitting the baseline
    }

    return currentSpread;
}