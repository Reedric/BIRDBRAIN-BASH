using UnityEngine;
using UnityEngine.UI;

// Applies smooth sin-wave-based wobble effects to a UI Image (or any RectTransform).
// Drives position, rotation, and scale via the RectTransform — no vertex editing needed.
// Works with:  UI Image, RawImage, or literally any GameObject with a RectTransform.

public class ImageWobble : MonoBehaviour
{

    // Wave Shape Settings
    [Header("Wave Shape")]
    [Tooltip("Master speed multiplier applied to every enabled effect.")]
    [Range(0f, 10f)]
    public float masterSpeed = 1f;


    // Vertical (Y) Wobble

    [Header("Vertical Wobble (Y position)")]
    public bool verticalEnabled = true;

    [Tooltip("Peak vertical displacement in Unity units (pixels in Screen Space - Overlay).")]
    [Range(0f, 500f)]
    public float verticalAmplitude = 20f;

    [Tooltip("Speed of the vertical wave.")]
    [Range(0f, 10f)]
    public float verticalSpeed = 1f;

    [Tooltip("Starting phase offset of the vertical wave (radians).")]
    [Range(0f, Mathf.PI * 2f)]
    public float verticalPhaseShift = 0f;

    //  Horizontal (X) Wobble

    [Header("─── Horizontal Wobble (X position) ────────────────")]
    public bool horizontalEnabled = false;

    [Tooltip("Peak horizontal displacement in Unity units.")]
    [Range(0f, 500f)]
    public float horizontalAmplitude = 10f;

    [Tooltip("Speed of the horizontal wave.")]
    [Range(0f, 10f)]
    public float horizontalSpeed = 1.5f;

    [Tooltip("Starting phase offset of the horizontal wave (radians). " +
             "Setting this to π/2 relative to vertical creates a circular orbit.")]
    [Range(0f, Mathf.PI * 2f)]
    public float horizontalPhaseShift = Mathf.PI * 0.5f;

    //  Rotation Wobble

    [Header("─── Rotation Wobble ─────────────────────────────────")]
    public bool rotationEnabled = true;

    [Tooltip("Peak rotation in degrees (positive = counter-clockwise at wave peak).")]
    [Range(0f, 180f)]
    public float rotationAmplitude = 15f;

    [Tooltip("Speed of the rotation wave.")]
    [Range(0f, 10f)]
    public float rotationSpeed = 0.8f;

    [Tooltip("Starting phase offset of the rotation wave (radians).")]
    [Range(0f, Mathf.PI * 2f)]
    public float rotationPhaseShift = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Scale Wobble
    // ─────────────────────────────────────────────────────────────────────────

    [Header("─── Scale Wobble ────────────────────────────────────")]
    public bool scaleEnabled = false;

    [Tooltip("How much the image grows/shrinks from its base size. " +
             "0.1 = ±10% size variation.")]
    [Range(0f, 2f)]
    public float scaleAmplitude = 0.15f;

    [Tooltip("Speed of the scale wave.")]
    [Range(0f, 10f)]
    public float scaleSpeed = 1.2f;

    [Tooltip("Starting phase offset of the scale wave (radians).")]
    [Range(0f, Mathf.PI * 2f)]
    public float scalePhaseShift = Mathf.PI;

    [Tooltip("If enabled, X and Y scale wobble independently, creating a 'squish' effect. " +
             "If disabled, both axes scale together uniformly.")]
    public bool squishMode = false;

    [Tooltip("Only used when Squish Mode is on. " +
             "Offsets the Y scale wave so X and Y are out of phase (more cartoony).")]
    [Range(0f, Mathf.PI * 2f)]
    public float squishPhaseOffset = Mathf.PI * 0.5f;


    //  Private state

    private RectTransform _rect;
    private Vector2       _baseAnchoredPosition;
    private Quaternion    _baseRotation;
    private Vector3       _baseScale;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        if (_rect == null)
        {
            Debug.LogError("[ImageWobble] No RectTransform found on " + gameObject.name +
                           ". This script requires a RectTransform (UI element).");
            enabled = false;
            return;
        }

        // Snapshot the 'rest' transform so wobble is always an offset from it,
        // never accumulating drift over time.
        _baseAnchoredPosition = _rect.anchoredPosition;
        _baseRotation         = _rect.localRotation;
        _baseScale            = _rect.localScale;
    }

    private void OnDisable()
    {
        // Restore the rest pose when the component is toggled off in-editor.
        if (_rect == null) return;
        _rect.anchoredPosition = _baseAnchoredPosition;
        _rect.localRotation    = _baseRotation;
        _rect.localScale       = _baseScale;
    }

    private void OnEnable()
    {
        // Re-snapshot in case the transform was changed while disabled.
        if (_rect == null) return;
        _baseAnchoredPosition = _rect.anchoredPosition;
        _baseRotation         = _rect.localRotation;
        _baseScale            = _rect.localScale;
    }

    private void Update()
    {
        float time = Time.time * masterSpeed;

        float deltaX = horizontalEnabled
            ? Mathf.Sin(time * horizontalSpeed * Mathf.PI * 2f + horizontalPhaseShift)
              * horizontalAmplitude
            : 0f;

        float deltaY = verticalEnabled
            ? Mathf.Sin(time * verticalSpeed * Mathf.PI * 2f + verticalPhaseShift)
              * verticalAmplitude
            : 0f;

        _rect.anchoredPosition = _baseAnchoredPosition + new Vector2(deltaX, deltaY);

        if (rotationEnabled)
        {
            float angle = Mathf.Sin(time * rotationSpeed * Mathf.PI * 2f + rotationPhaseShift)
                          * rotationAmplitude;
            _rect.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, angle);
        }
        else
        {
            _rect.localRotation = _baseRotation;
        }

        if (scaleEnabled)
        {
            float sinScale = Mathf.Sin(time * scaleSpeed * Mathf.PI * 2f + scalePhaseShift);

            Vector3 scale;
            if (squishMode)
            {
                // X and Y oscillate out of phase — cartoony squish & stretch
                float sinScaleY = Mathf.Sin(time * scaleSpeed * Mathf.PI * 2f
                                            + scalePhaseShift + squishPhaseOffset);
                scale = new Vector3(
                    _baseScale.x * (1f + sinScale  * scaleAmplitude),
                    _baseScale.y * (1f + sinScaleY * scaleAmplitude),
                    _baseScale.z
                );
            }
            else
            {
                // Uniform pulse
                float factor = 1f + sinScale * scaleAmplitude;
                scale = new Vector3(
                    _baseScale.x * factor,
                    _baseScale.y * factor,
                    _baseScale.z
                );
            }

            _rect.localScale = scale;
        }
        else
        {
            _rect.localScale = _baseScale;
        }
    }


    /// Re-snapshots the current transform as the new rest position.
    public void RefreshBasePose()
    {
        if (_rect == null) return;
        _baseAnchoredPosition = _rect.anchoredPosition;
        _baseRotation         = _rect.localRotation;
        _baseScale            = _rect.localScale;
    }

}
