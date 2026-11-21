using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Data;

namespace CardGame.Core
{
    /// <summary>Webcam capture and managed OpenCV DICT_4X4_50 ID 0-49 detection.</summary>
    public class WebCamArucoManager : MonoBehaviour
    {
        [Header("Webcam")]
        [SerializeField] private string requestedDeviceName = "";
        [SerializeField] private int requestedWidth = 1280;
        [SerializeField] private int requestedHeight = 720;
        [SerializeField] private int requestedFPS = 30;
        [Header("Display")]
        [SerializeField] private RawImage displayImage;
        [Header("Detection Settings")]
        [SerializeField, Min(160)] private int analysisSize = 640;
        [SerializeField, Min(3)] private int adaptiveBlockSize = 31;
        [SerializeField] private int adaptiveC = 8;
        [SerializeField, Min(50)] private float minContourArea = 200;
        [SerializeField, Range(0.01f, 0.9f)] private float maxContourAreaFraction = 0.5f;
        [SerializeField, Min(0)] private int skipFrames = 0;
        [Tooltip("No fresh frame for this long invalidates visibility and pending confirmation.")]
        [SerializeField, Min(0.1f)] private float cameraStallTimeout = 0.25f;
        [Header("Debug")]
        [SerializeField] private bool showDebugPanel = true;

        public event Action<int, Vector2> OnMarkerDetected;
        public event Action<int> OnMarkerLost;
        /// <summary>Only emitted for a newly analyzed camera frame; count may be 0, 1 or multiple.</summary>
        public event Action<int> OnScanFrameCompleted;
        public bool IsCameraRunning => _webCamTex != null && _webCamTex.isPlaying;
        public int DetectedMarkerID { get; private set; } = -1;
        public string ValidationStatus { get; private set; } = "Waiting for camera";
        public string LastError { get; private set; } = "";
        public float LastAnalysisTime { get; private set; } = -1;
        public float VisibilityTimeout => Mathf.Max(0.1f, cameraStallTimeout);

        private WebCamTexture _webCamTex;
        private Color32[] _pixels;
        private byte[] _gray;
        private int _frameCounter;
        private float _lastFreshFrame;
        private bool _waitingForPermission;
        private Vector3 _displayScale = Vector3.one;
        private readonly ArucoDetector _detector = new ArucoDetector();
        private readonly List<ArucoDetector.Detection> _detections = new List<ArucoDetector.Detection>();

        private System.Collections.IEnumerator Start()
        {
            if (displayImage != null) _displayScale = displayImage.rectTransform.localScale;
            _waitingForPermission = true;
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            _waitingForPermission = false;
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                SetError("Webcam permission was denied.");
                yield break;
            }
            StartCamera();
        }

        private void StartCamera()
        {
            try
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                if (devices.Length == 0) { SetError("No webcam device found."); return; }
                string device = devices[0].name;
                if (!string.IsNullOrEmpty(requestedDeviceName))
                {
                    bool found = false;
                    foreach (WebCamDevice candidate in devices)
                        if (candidate.name == requestedDeviceName) { device = candidate.name; found = true; break; }
                    if (!found) { SetError("Requested webcam was not found: " + requestedDeviceName); return; }
                }
                _webCamTex = new WebCamTexture(device, requestedWidth, requestedHeight, requestedFPS);
                _webCamTex.Play();
                _lastFreshFrame = Time.unscaledTime;
                ValidationStatus = "Waiting for first frame";
            }
            catch (Exception error) { SetError(error.Message); }
        }

        private void Update()
        {
            if (!IsCameraRunning)
            {
                if (displayImage != null) displayImage.enabled = false;
                InvalidateDetection();
                return;
            }
            if (displayImage != null)
            {
                displayImage.enabled = _webCamTex.width >= 32 && _webCamTex.height >= 32;
                displayImage.texture = _webCamTex;
                displayImage.rectTransform.localEulerAngles = new Vector3(0, 0, -_webCamTex.videoRotationAngle);
                displayImage.rectTransform.localScale = new Vector3(_displayScale.x,
                    _displayScale.y * (_webCamTex.videoVerticallyMirrored ? -1 : 1), _displayScale.z);
            }
            if (!_webCamTex.didUpdateThisFrame)
            {
                if (Time.unscaledTime - _lastFreshFrame > VisibilityTimeout)
                {
                    ValidationStatus = "Camera frame stalled";
                    InvalidateDetection();
                }
                return;
            }
            _lastFreshFrame = Time.unscaledTime;
            if (_frameCounter++ % (Mathf.Max(0, skipFrames) + 1) != 0) return;
            try { DetectMarkers(); }
            catch (Exception error) { SetError(error.Message); InvalidateDetection(); }
        }

        private void DetectMarkers()
        {
            int srcW = _webCamTex.width, srcH = _webCamTex.height;
            if (srcW < 32 || srcH < 32) { InvalidateDetection(); return; }
            int w = Mathf.Clamp(analysisSize, 160, srcW);
            int h = Mathf.Max(12, Mathf.RoundToInt((float)w * srcH / srcW));
            if (_pixels == null || _pixels.Length != srcW * srcH) _pixels = new Color32[srcW * srcH];
            if (_gray == null || _gray.Length != w * h) _gray = new byte[w * h];
            _webCamTex.GetPixels32(_pixels);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Unity pixels are bottom-up; detector consumes top-down, mirror-corrected pixels.
                    int rawY = y * srcH / h;
                    if (!_webCamTex.videoVerticallyMirrored) rawY = srcH - 1 - rawY;
                    Color32 pixel = _pixels[rawY * srcW + x * srcW / w];
                    _gray[y * w + x] = (byte)((pixel.r * 77 + pixel.g * 150 + pixel.b * 29) >> 8);
                }
            _detector.Detect(_gray, w, h, Mathf.Max(3, adaptiveBlockSize) | 1, adaptiveC,
                Mathf.Max(50, minContourArea), Mathf.Clamp(maxContourAreaFraction, 0.01f, 0.9f), _detections);
            LastAnalysisTime = Time.unscaledTime;
            if (_detections.Count != 1)
            {
                ValidationStatus = _detections.Count == 0 ? "No valid marker (exact match required)" : "Multiple markers: scan one card";
                InvalidateDetection();
                OnScanFrameCompleted?.Invoke(_detections.Count);
                return;
            }
            ArucoDetector.Detection detection = _detections[0];
            if (DetectedMarkerID != detection.ID) InvalidateDetection();
            DetectedMarkerID = detection.ID;
            ValidationStatus = $"Exact dictionary match; border/margin OK; contrast {detection.Contrast:F0}/255";
            Vector2 position = new Vector2(detection.Center.X / w, 1 - detection.Center.Y / h);
            // Same clockwise rotation used by the RawImage, in normalized display coordinates.
            switch ((_webCamTex.videoRotationAngle % 360 + 360) % 360)
            {
                case 90: position = new Vector2(position.y, 1 - position.x); break;
                case 180: position = Vector2.one - position; break;
                case 270: position = new Vector2(1 - position.y, position.x); break;
            }
            OnMarkerDetected?.Invoke(detection.ID, position);
            OnScanFrameCompleted?.Invoke(1);
        }

        private void InvalidateDetection()
        {
            if (DetectedMarkerID < 0) return;
            int previous = DetectedMarkerID;
            DetectedMarkerID = -1;
            OnMarkerLost?.Invoke(previous);
        }

        private void SetError(string error)
        {
            LastError = error;
            ValidationStatus = "Camera/detection error";
            Debug.LogError("[WebCamAruco] " + error);
        }

        private void OnDisable() { InvalidateDetection(); }
        private void OnDestroy()
        {
            InvalidateDetection();
            if (_webCamTex == null) return;
            if (_webCamTex.isPlaying) _webCamTex.Stop();
            Destroy(_webCamTex);
        }

        private void OnGUI()
        {
            if (!showDebugPanel) return;
            GUILayout.BeginArea(new Rect(12, 12, 470, 240), GUI.skin.box);
            GUILayout.Label("ArUco Card Scanner — DICT_4X4_50 / 0–49");
            GUILayout.Label("Webcam: " + (_waitingForPermission ? "Permission pending" : IsCameraRunning ?
                (Time.unscaledTime - _lastFreshFrame <= VisibilityTimeout ? "Receiving frames" : "No fresh frame") : "Stopped / unavailable"));
            GUILayout.Label("Marker ID: " + (DetectedMarkerID < 0 ? "—" : DetectedMarkerID.ToString()));
            CardData card = CardDatabaseManager.Instance?.GetCardByID(DetectedMarkerID);
            GUILayout.Label("Card: " + (card != null ? card.cardName : "—"));
            GUILayout.Label("Validation: " + ValidationStatus);
            GUILayout.Label("Gameplay: " + (GameManager.Instance != null ?
                GameManager.Instance.GetMarkerStatus(DetectedMarkerID) : "GameManager missing"));
            GUILayout.Label("Last error: " + (string.IsNullOrEmpty(LastError) ? "—" : LastError));
            if (GUILayout.Button("Hide debug panel")) showDebugPanel = false;
            GUILayout.EndArea();
        }
    }
}
