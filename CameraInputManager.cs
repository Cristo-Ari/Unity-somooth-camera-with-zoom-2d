using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraInputManager : MonoBehaviour {
	public event Action<Vector2> OnMouseMoved;
	public event Action<bool, Vector2> OnMiddleButtonChanged;
	public event Action<Vector2> OnMouseScrolled;
	InputAction mousePositionAction;
	InputAction middleButtonAction;
	InputAction scrollAction;

	void OnEnable() {
		mousePositionAction = new InputAction(
			binding: "<Pointer>/position",
			type: InputActionType.PassThrough
		);
		middleButtonAction = new InputAction(
			binding: "<Mouse>/middleButton",
			type: InputActionType.Button
		);
		scrollAction = new InputAction(
			binding: "<Mouse>/scroll",
			type: InputActionType.PassThrough
		);
		mousePositionAction.performed += context => {
			Vector2 position = context.ReadValue<Vector2>();
			OnMouseMoved?.Invoke(
				obj: position
			);
		};
		middleButtonAction.performed += context => {
			bool isPressed = context.ReadValue<float>() > 0.5f;
			Vector2 position = mousePositionAction.ReadValue<Vector2>();
			OnMiddleButtonChanged?.Invoke(
				arg1: isPressed,
				arg2: position
			);
		};
		middleButtonAction.canceled += context => {
			Vector2 position = mousePositionAction.ReadValue<Vector2>();
			OnMiddleButtonChanged?.Invoke(
				arg1: false,
				arg2: position
			);
		};
		scrollAction.performed += context => {
			Vector2 scrollDelta = context.ReadValue<Vector2>();
			OnMouseScrolled?.Invoke(
				obj: scrollDelta
			);
		};
		mousePositionAction.Enable();
		middleButtonAction.Enable();
		scrollAction.Enable();
	}

	void OnDisable() {
		mousePositionAction.Disable();
		middleButtonAction.Disable();
		scrollAction.Disable();
	}
}
