// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Common;
using Niantic.Lightship.AR.Subsystems.ObjectDetection;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.ObjectDetection
{
    /// <summary>
    /// The manager for the object detection subsystem.
    /// </summary>
    [PublicAPI]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(LightshipARUpdateOrder.ObjectDetectionManager)]
    public class ARObjectDetectionManager:
        SubsystemLifecycleManager<XRObjectDetectionSubsystem, XRObjectDetectionSubsystemDescriptor, XRObjectDetectionSubsystem.Provider>
    {
        [SerializeField]
        [Tooltip("Frame rate that the object detection algorithm will aim to run at")]
        [Range(1, 90)]
        private uint _targetFrameRate = LightshipObjectDetectionSubsystem.MaxRecommendedFrameRate;

        /// <summary>
        /// Frame rate that the object detection inference will aim to run at.
        /// </summary>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public uint TargetFrameRate
        {
            get => subsystem?.TargetFrameRate ?? _targetFrameRate;
            set
            {
                if (value <= 0)
                {
                    Log.Error("Target frame rate value must be greater than zero.");
                    return;
                }

                _targetFrameRate = value;
                if (subsystem != null)
                {
                    subsystem.TargetFrameRate = value;
                }
            }
        }

        /// <summary>
        /// The names of the object detection categories that the current model is able to detect.
        /// Will return an empty list if no metadata is available.
        /// </summary>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public IReadOnlyList<string> CategoryNames
        {
            get
            {
                if (subsystem != null && subsystem.TryGetCategoryNames(out var names))
                {
                    return names;
                }

                return new List<string>();
            }
        }

        /// <summary>
        /// True if the underlying subsystem has finished initialization.
        /// </summary>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public bool IsMetadataAvailable => subsystem?.IsMetadataAvailable ?? false;

        /// <summary>
        /// An event which fires when the underlying subsystem has finished initializing.
        /// </summary>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public event Action<ARObjectDetectionModelEventArgs> MetadataInitialized
        {
            add
            {
                _metadataInitialized += value;
                if (IsMetadataAvailable)
                {
                    var args =
                        new ARObjectDetectionModelEventArgs
                        {
                            CategoryNames = CategoryNames
                        };

                    value.Invoke(args);
                }
            }
            remove
            {
                _metadataInitialized -= value;
            }
        }

        private Action<ARObjectDetectionModelEventArgs> _metadataInitialized;
        private bool _sentMetadataInitializedEvent;

        /// <summary>
        /// An event which fires when the underlying subsystem has made the set of detected objects for the
        /// latest input camera frame available.
        /// </summary>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public event Action<ARObjectDetectionsUpdatedEventArgs> ObjectDetectionsUpdated;

        /// <summary>
        /// Tries to acquire the most recent set of detected objects.
        /// </summary>
        /// <param name="results">An array of detected objects.If no objects were
        /// detected, this array will be empty.</param>
        /// <returns>True if the object detection neural network has produced output.</returns>
        /// <remarks>
        /// This is an experimental API. Experimental features are subject to breaking changes,
        /// not officially supported, and may be deprecated without notice.
        /// </remarks>
        public bool TryGetDetectedObjects(out XRDetectedObject[] results)
        {
            if (subsystem == null)
            {
                results = Array.Empty<XRDetectedObject>();
                return false;
            }

            return subsystem.TryGetDetectedObjects(out results);
        }

        /// <summary>
        /// The frame id of the last seen object detection results set.
        /// </summary>
        private uint? _lastKnownFrameId;

        /// <summary>
        ///
        /// </summary>
        private float _timeOfLastUpdate;

        /// <summary>
        /// Callback before the subsystem is started (but after it is created).
        /// </summary>
        protected override void OnBeforeStart()
        {
            TargetFrameRate = _targetFrameRate;
        }

        /// <summary>
        /// Callback when the manager is being disabled.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();
            _sentMetadataInitializedEvent = false;
        }

        /// <summary>
        /// Callback as the manager is being updated.
        /// </summary>
        private void Update()
        {
            if (subsystem == null)
            {
                return;
            }

            TargetFrameRate = _targetFrameRate;

            if (!subsystem.running || !subsystem.IsMetadataAvailable)
            {
                return;
            }

            if (!_sentMetadataInitializedEvent)
            {
                var args = new ARObjectDetectionModelEventArgs { CategoryNames = CategoryNames };
                if (args.CategoryNames.Count > 0)
                {
                    _sentMetadataInitializedEvent = true;
                    _metadataInitialized?.Invoke(args);
                }
                else
                {
                    Log.Error("Unexpected state: Object detection metadata is available but category names were not.");
                    return;
                }
            }

            var currentFrameId = subsystem.LatestFrameId;
            if (currentFrameId != _lastKnownFrameId)
            {
                _lastKnownFrameId = currentFrameId;
                _timeOfLastUpdate = Time.unscaledTime;
                // TODO(ARDK-2679): Send a bool with this event signaling whether the frame was predicted or warped
                InvokeFrameReceived();
            }

        }

        private void InvokeFrameReceived()
        {
            if (ObjectDetectionsUpdated == null)
            {
                return;
            }

            if (subsystem.TryGetDetectedObjects(out var results))
            {
                var args = new ARObjectDetectionsUpdatedEventArgs { Results = results };
                ObjectDetectionsUpdated.Invoke(args);
            }
        }
    }
}