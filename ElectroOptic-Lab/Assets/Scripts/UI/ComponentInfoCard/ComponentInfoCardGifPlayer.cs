using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ThreeDISevenZeroR.UnityGifDecoder;
using UnityEngine;
using UnityEngine.Networking;

namespace ElectroOptics.UI.ComponentInfoCard
{
    public enum ComponentInfoCardGifState
    {
        Idle,
        Loading,
        Playing,
        Missing,
        Failed
    }

    /// <summary>
    /// Streams and decodes one component preview GIF at a time. Decoding happens on a worker thread;
    /// Texture2D creation and updates remain on Unity's main thread.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComponentInfoCardGifPlayer : MonoBehaviour
    {
        public const string StreamingAssetsFolderName = "ComponentInfoCard";
        public const long RecommendedMaxBytes = 5L * 1024L * 1024L;

        [SerializeField] private ComponentInfoCardView cardView;
        [SerializeField, Range(1, 8)] private int frameBufferCapacity = 3;

        private BlockingCollection<DecodedFrame> frames;
        private CancellationTokenSource cancellation;
        private Task decoderTask;
        private Coroutine loadCoroutine;
        private Texture2D texture;
        private DecodedFrame currentFrame;
        private float frameTimer;
        private volatile int generation;
        private readonly object failureSync = new object();
        private string pendingDecoderFailure;
        private int pendingFailureGeneration;

        public ComponentInfoCardGifState State { get; private set; } = ComponentInfoCardGifState.Idle;
        public Texture CurrentTexture => texture;
        public string CurrentFileName { get; private set; } = string.Empty;
        public bool IsValid => cardView != null && cardView.IsValid && frameBufferCapacity == 3;

        public void Configure(ComponentInfoCardView view, int capacity = 3)
        {
            cardView = view;
            frameBufferCapacity = Mathf.Clamp(capacity, 1, 8);
        }

        public void Play(string fileName)
        {
            Stop();
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            CurrentFileName = fileName.Trim();
            if (!IsSafeGifFileName(CurrentFileName))
            {
                State = ComponentInfoCardGifState.Failed;
                Warn($"GIF 文件名无效：{CurrentFileName}");
                cardView?.ClearAnimatedPreview();
                return;
            }

            int playGeneration = ++generation;
            State = ComponentInfoCardGifState.Loading;
            loadCoroutine = StartCoroutine(LoadBytes(playGeneration, CurrentFileName));
        }

        public void Stop()
        {
            generation++;
            ReleaseAsyncResources();
            ReleaseTexture();
            cardView?.ClearAnimatedPreview();
            CurrentFileName = string.Empty;
            State = ComponentInfoCardGifState.Idle;
        }

        public static string GetStreamingAssetPath(string fileName)
        {
            return Path.Combine(Application.streamingAssetsPath, StreamingAssetsFolderName, fileName);
        }

        public static bool IsSafeGifFileName(string fileName)
        {
            return !string.IsNullOrWhiteSpace(fileName)
                   && string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
                   && string.Equals(Path.GetExtension(fileName), ".gif", StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerator LoadBytes(int playGeneration, string fileName)
        {
            string path = GetStreamingAssetPath(fileName);
            bool isLocalFile = !path.Contains("://");
            if (isLocalFile && !File.Exists(path))
            {
                SetLoadFailureIfCurrent(playGeneration, ComponentInfoCardGifState.Missing, fileName + "：文件不存在");
                loadCoroutine = null;
                yield break;
            }

            string requestPath = isLocalFile ? new Uri(Path.GetFullPath(path)).AbsoluteUri : path;
            using (UnityWebRequest request = UnityWebRequest.Get(requestPath))
            {
                yield return request.SendWebRequest();
                if (playGeneration != generation)
                    yield break;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    ComponentInfoCardGifState state = request.responseCode == 404
                        ? ComponentInfoCardGifState.Missing
                        : ComponentInfoCardGifState.Failed;
                    SetLoadFailureIfCurrent(playGeneration, state, fileName + "：" + request.error);
                    loadCoroutine = null;
                    yield break;
                }

                byte[] bytes = request.downloadHandler.data;
                if (bytes == null || bytes.Length == 0)
                {
                    SetLoadFailureIfCurrent(playGeneration, ComponentInfoCardGifState.Failed, fileName + "：文件为空");
                    loadCoroutine = null;
                    yield break;
                }

                if (bytes.LongLength > RecommendedMaxBytes)
                {
                    Warn($"GIF `{fileName}` 为 {bytes.LongLength / (1024f * 1024f):F1} MB，超过建议的 5 MB 上限。");
                }

                StartDecode(playGeneration, fileName, bytes);
            }

            loadCoroutine = null;
        }

        private void StartDecode(int playGeneration, string fileName, byte[] bytes)
        {
            cancellation = new CancellationTokenSource();
            BlockingCollection<DecodedFrame> frameQueue = new BlockingCollection<DecodedFrame>(
                new ConcurrentQueue<DecodedFrame>(),
                frameBufferCapacity);
            frames = frameQueue;
            CancellationToken token = cancellation.Token;
            decoderTask = Task.Run(
                () => DecodeLoop(playGeneration, fileName, bytes, frameQueue, token),
                token);
        }

        private void DecodeLoop(
            int playGeneration,
            string fileName,
            byte[] bytes,
            BlockingCollection<DecodedFrame> queue,
            CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && playGeneration == generation)
                {
                    int decodedCount = 0;
                    using (GifStream gif = new GifStream(bytes))
                    {
                        while (gif.HasMoreData && !token.IsCancellationRequested && playGeneration == generation)
                        {
                            if (gif.CurrentToken == GifStream.Token.Image)
                            {
                                ThreeDISevenZeroR.UnityGifDecoder.Model.GifImage image = gif.ReadImage();
                                Color32[] pixels = new Color32[image.colors.Length];
                                Array.Copy(image.colors, pixels, pixels.Length);
                                queue.Add(
                                    new DecodedFrame(
                                        gif.Header.width,
                                        gif.Header.height,
                                        pixels,
                                        (float)Math.Max(0.01, image.SafeDelaySeconds)),
                                    token);
                                decodedCount++;
                            }
                            else
                            {
                                gif.SkipToken();
                            }
                        }
                    }

                    if (decodedCount == 0)
                        throw new InvalidDataException("GIF 中没有可播放图像帧");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception exception)
            {
                if (token.IsCancellationRequested || playGeneration != generation)
                    return;

                lock (failureSync)
                {
                    pendingFailureGeneration = playGeneration;
                    pendingDecoderFailure = fileName + "：" + exception.Message;
                }
            }
        }

        private void Update()
        {
            string failure = null;
            int failureGeneration = 0;
            lock (failureSync)
            {
                if (!string.IsNullOrEmpty(pendingDecoderFailure))
                {
                    failure = pendingDecoderFailure;
                    failureGeneration = pendingFailureGeneration;
                    pendingDecoderFailure = null;
                }
            }

            if (!string.IsNullOrEmpty(failure) && failureGeneration == generation)
            {
                ReleaseAsyncResources();
                ReleaseTexture();
                cardView?.ClearAnimatedPreview();
                State = ComponentInfoCardGifState.Failed;
                Warn("GIF 解码失败：" + failure);
                return;
            }

            if (State == ComponentInfoCardGifState.Failed
                || State == ComponentInfoCardGifState.Missing
                || frames == null)
            {
                return;
            }

            if (currentFrame == null)
            {
                if (frames.TryTake(out DecodedFrame first))
                    ShowFrame(first);
                return;
            }

            frameTimer += Time.unscaledDeltaTime;
            if (frameTimer < currentFrame.DelaySeconds)
                return;

            if (!frames.TryTake(out DecodedFrame next))
                return;

            frameTimer -= currentFrame.DelaySeconds;
            ShowFrame(next);
        }

        private void ShowFrame(DecodedFrame frame)
        {
            if (frame == null || frame.Pixels == null)
                return;

            if (texture == null || texture.width != frame.Width || texture.height != frame.Height)
            {
                ReleaseTexture();
                texture = new Texture2D(frame.Width, frame.Height, TextureFormat.RGBA32, false, false)
                {
                    name = "ComponentInfoCardGifFrame",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            texture.SetPixels32(frame.Pixels);
            texture.Apply(false, false);
            currentFrame = frame;
            State = ComponentInfoCardGifState.Playing;
            cardView?.SetAnimatedPreview(texture);
        }

        private void SetLoadFailureIfCurrent(
            int playGeneration,
            ComponentInfoCardGifState state,
            string message)
        {
            if (playGeneration != generation)
                return;

            State = state;
            cardView?.ClearAnimatedPreview();
            Warn(message);
        }

        private void ReleaseAsyncResources()
        {
            if (loadCoroutine != null)
            {
                StopCoroutine(loadCoroutine);
                loadCoroutine = null;
            }

            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
                cancellation = null;
            }

            if (frames != null)
            {
                try
                {
                    frames.CompleteAdding();
                }
                catch (ObjectDisposedException)
                {
                }

                frames.Dispose();
                frames = null;
            }

            decoderTask = null;
            currentFrame = null;
            frameTimer = 0f;
            lock (failureSync)
            {
                pendingDecoderFailure = null;
                pendingFailureGeneration = 0;
            }
        }

        private void ReleaseTexture()
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
                Destroy(texture);
            else
                DestroyImmediate(texture);
            texture = null;
        }

        private void Warn(string message)
        {
            Debug.LogWarning("[ComponentInfoCard] " + message, this);
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            Stop();
        }

        private sealed class DecodedFrame
        {
            public int Width { get; }
            public int Height { get; }
            public Color32[] Pixels { get; }
            public float DelaySeconds { get; }

            public DecodedFrame(int width, int height, Color32[] pixels, float delaySeconds)
            {
                Width = width;
                Height = height;
                Pixels = pixels;
                DelaySeconds = delaySeconds;
            }
        }
    }
}
