using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControl : MonoBehaviour
{
    Camera mainCameraInstance;
    InputAction mousePositionInputAction;
    InputAction middleMouseButtonInputAction;
    InputAction mouseScrollInputAction;
    
    bool isDraggingCamera;
    Vector2 previousMouseScreenPosition;
    float currentZoomVelocity;
    Vector2 scrollStartScreenPosition;
    
    const float orthographicMovementScaleFactor = 1f;
    const float perspectiveMovementScaleFactor = 0.01f;
    const float zoomPercentagePerScrollStep = 1f;
    const float orthographicZoomDepthOffset = 0.01f;
    const float zoomScrollDampingSpeed = 5f;
    const float defaultPerspectiveZoomDistance = 10f;

    readonly List<CameraMovementRecord> recordedCameraMovementRecordsForInertia = new();
    Vector3 currentInertiaMovementVelocity = Vector3.zero;
    const float inertiaAveragingTimeWindowInSeconds = 0.1f;
    const float inertiaMovementDampingSpeed = 3f;

    struct CameraMovementRecord
    {
        public Vector3 RecordedMovementVelocity;
        public float RecordedTimeStamp;
    }

    void Awake()
    {
        mainCameraInstance = Camera.main;
    }

    void OnEnable()
    {
        mousePositionInputAction = new InputAction(binding: "<Pointer>/position");
        middleMouseButtonInputAction = new InputAction(binding: "<Mouse>/middleButton");
        mouseScrollInputAction = new InputAction(binding: "<Mouse>/scroll");

        mousePositionInputAction.Enable();
        middleMouseButtonInputAction.Enable();
        mouseScrollInputAction.Enable();
    }

    void OnDisable()
    {
        mousePositionInputAction.Disable();
        middleMouseButtonInputAction.Disable();
        mouseScrollInputAction.Disable();
    }

    void Update()
    {
        HandleCameraDragInput();
        HandleCameraInertia();
        HandleCameraZoomInput();
        ProcessCameraZoom();
    }

    void HandleCameraDragInput()
    {
        float middleButtonState = middleMouseButtonInputAction.ReadValue<float>();
        
        // Начало перетаскивания
        if (middleButtonState >= 0.5f && !isDraggingCamera)
        {
            isDraggingCamera = true;
            previousMouseScreenPosition = mousePositionInputAction.ReadValue<Vector2>();
            recordedCameraMovementRecordsForInertia.Clear();
            currentInertiaMovementVelocity = Vector3.zero;
        }
        // Окончание перетаскивания
        else if (middleButtonState < 0.5f && isDraggingCamera)
        {
            isDraggingCamera = false;
            CalculateInertia();
        }

        // Обработка перемещения камеры
        if (isDraggingCamera)
        {
            Vector2 currentMousePosition = mousePositionInputAction.ReadValue<Vector2>();
            Vector2 mouseDelta = currentMousePosition - previousMouseScreenPosition;

            Vector3 movementDelta = mainCameraInstance.orthographic 
                ? ComputeOrthographicMovementDelta(mouseDelta) 
                : ComputePerspectiveMovementDelta(mouseDelta);

            mainCameraInstance.transform.position += movementDelta;
            
            // Запись скорости каждый кадр
            if (Time.deltaTime > 0)
            {
                Vector3 frameVelocity = movementDelta / Time.deltaTime;
                recordedCameraMovementRecordsForInertia.Add(new CameraMovementRecord
                {
                    RecordedMovementVelocity = frameVelocity,
                    RecordedTimeStamp = Time.time
                });
            }

            previousMouseScreenPosition = currentMousePosition;
        }
    }

    void HandleCameraInertia()
    {
        if (!isDraggingCamera && currentInertiaMovementVelocity != Vector3.zero)
        {
            mainCameraInstance.transform.position += currentInertiaMovementVelocity * Time.deltaTime;
            currentInertiaMovementVelocity = Vector3.Lerp(
                currentInertiaMovementVelocity, 
                Vector3.zero, 
                inertiaMovementDampingSpeed * Time.deltaTime
            );
        }
    }

    void CalculateInertia()
    {
        List<CameraMovementRecord> validRecords = new();
        float currentTime = Time.time;

        foreach (var record in recordedCameraMovementRecordsForInertia)
        {
            if (currentTime - record.RecordedTimeStamp <= inertiaAveragingTimeWindowInSeconds)
                validRecords.Add(record);
        }

        if (validRecords.Count > 0)
        {
            Vector3 totalVelocity = Vector3.zero;
            foreach (var record in validRecords)
                totalVelocity += record.RecordedMovementVelocity;
            
            currentInertiaMovementVelocity = totalVelocity / validRecords.Count;
        }
    }

    void HandleCameraZoomInput()
    {
        Vector2 scrollDelta = mouseScrollInputAction.ReadValue<Vector2>();
        if (scrollDelta != Vector2.zero)
        {
            scrollStartScreenPosition = mousePositionInputAction.ReadValue<Vector2>();
            currentZoomVelocity = scrollDelta.y;
        }
    }

    void ProcessCameraZoom()
    {
        float zoomFactor = 1f + (currentZoomVelocity * zoomPercentagePerScrollStep * Time.deltaTime);
        
        if (mainCameraInstance.orthographic)
            ApplyOrthographicZoom(zoomFactor);
        else
            ApplyPerspectiveZoom(zoomFactor);

        currentZoomVelocity = Mathf.Lerp(
            currentZoomVelocity, 
            0f, 
            Time.deltaTime * zoomScrollDampingSpeed
        );
    }

    Vector3 ComputeOrthographicMovementDelta(Vector2 mouseScreenDelta)
    {
        float scaleFactor = mainCameraInstance.orthographicSize / (Screen.height / 2f);
        return new Vector3(
            -mouseScreenDelta.x * scaleFactor * orthographicMovementScaleFactor,
            -mouseScreenDelta.y * scaleFactor * orthographicMovementScaleFactor,
            0f
        );
    }

    Vector3 ComputePerspectiveMovementDelta(Vector2 mouseScreenDelta)
    {
        Vector3 moveDirection = (mainCameraInstance.transform.right * -mouseScreenDelta.x) 
                             + (mainCameraInstance.transform.up * -mouseScreenDelta.y);
        return moveDirection * perspectiveMovementScaleFactor;
    }

    void ApplyOrthographicZoom(float zoomFactor)
    {
        Vector3 screenPoint = new Vector3(
            scrollStartScreenPosition.x,
            scrollStartScreenPosition.y,
            orthographicZoomDepthOffset
        );

        Vector3 worldBefore = mainCameraInstance.ScreenToWorldPoint(screenPoint);
        mainCameraInstance.orthographicSize /= zoomFactor;
        Vector3 worldAfter = mainCameraInstance.ScreenToWorldPoint(screenPoint);
        mainCameraInstance.transform.position += worldBefore - worldAfter;
    }

    void ApplyPerspectiveZoom(float zoomFactor)
    {
        Vector3 screenPoint = new Vector3(
            scrollStartScreenPosition.x,
            scrollStartScreenPosition.y,
            defaultPerspectiveZoomDistance
        );

        Vector3 worldBefore = mainCameraInstance.ScreenToWorldPoint(screenPoint);
        Vector3 forward = mainCameraInstance.transform.forward;
        float distance = Vector3.Distance(mainCameraInstance.transform.position, worldBefore);
        
        mainCameraInstance.transform.position += forward * (distance * (1f - 1f/zoomFactor));
        
        Vector3 worldAfter = mainCameraInstance.ScreenToWorldPoint(screenPoint);
        mainCameraInstance.transform.position += worldBefore - worldAfter;
    }
}