using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ThreeDISevenZeroR.UnityGifDecoder;
using UnityEngine;
using UnityEngine.Networking;

namespace ElectroOptics.UI.ExperimentGuide
{
    public enum GuideGifLoadState
    {
        Idle,
        Loading,
        Playing,
        Missing,
        Failed
    }

    public interface IGuideGifPlayer
    {
        GuideGifLoadState State { get; }
        Texture CurrentTexture { get; }
        event Action<Texture> FrameChanged;
        event Action<GuideGifLoadState, string> StateChanged;
        void Open(Scene2GuideStageId stageId, string fileName);
        void Close();
    }

    [DisallowMultipleComponent]
    public sealed class Scene2GuideGifPlayer : MonoBehaviour, IGuideGifPlayer
    {
        private const long RecommendedMaxBytes = 20L * 1024L * 1024L;

        private BlockingCollection<DecodedFrame> frames;
        private CancellationTokenSource cancellation;
        private Task decoderTask;
        private Coroutine loadCoroutine;
        private Texture2D texture;
        private DecodedFrame currentFrame;
        private float frameTimer;
        private volatile int generation;
        private int frameBufferCapacity = 3;
        private readonly object failureSync = new object();
        private string pendingDecoderFailure;
        private string pendingDecoderLog;

        public GuideGifLoadState State { get; private set; } = GuideGifLoadState.Idle;
        public Texture CurrentTexture => texture;
        public event Action<Texture> FrameChanged;
        public event Action<GuideGifLoadState, string> StateChanged;

        public void Configure(int capacity)
        {
            frameBufferCapacity = Mathf.Clamp(capacity, 1, 8);
        }

        public void Open(Scene2GuideStageId stageId, string fileName)
        {
            Close();
            int openGeneration = ++generation;
            State = GuideGifLoadState.Loading;
            StateChanged?.Invoke(State, string.Empty);
            loadCoroutine = StartCoroutine(LoadBytes(openGeneration, stageId, fileName));
        }

        public void Close()
        {
            generation++;
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
                pendingDecoderLog = null;
            }

            if (texture != null)
            {
                Destroy(texture);
                texture = null;
                FrameChanged?.Invoke(null);
            }

            State = GuideGifLoadState.Idle;
        }

        private IEnumerator LoadBytes(int openGeneration, Scene2GuideStageId stageId, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                SetStateIfCurrent(openGeneration, GuideGifLoadState.Missing, string.Empty);
                yield break;
            }

            string path = Path.Combine(Application.streamingAssetsPath, "ExperimentGuide", fileName);
            bool isLocalFile = !path.Contains("://");
            if (isLocalFile && !File.Exists(path))
            {
                SetStateIfCurrent(openGeneration, GuideGifLoadState.Missing, fileName);
                yield break;
            }

            string requestPath = isLocalFile ? new Uri(path).AbsoluteUri : path;
            using (UnityWebRequest request = UnityWebRequest.Get(requestPath))
            {
                yield return request.SendWebRequest();
                if (openGeneration != generation)
                    yield break;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    GuideGifLoadState failureState = request.responseCode == 404
                        ? GuideGifLoadState.Missing
                        : GuideGifLoadState.Failed;
                    SetStateIfCurrent(openGeneration, failureState, fileName + "：" + request.error);
                    yield break;
                }

                byte[] bytes = request.downloadHandler.data;
                if (bytes == null || bytes.Length == 0)
                {
                    SetStateIfCurrent(openGeneration, GuideGifLoadState.Failed, fileName + "：文件为空");
                    yield break;
                }

                if (bytes.LongLength > RecommendedMaxBytes)
                {
                    Debug.LogWarning(
                        $"[Scene2RealtimeGuide] GIF `{fileName}` 为 {bytes.LongLength / (1024f * 1024f):F1}MB，超过建议的 20MB 上限。",
                        this);
                }

                StartDecode(openGeneration, stageId, fileName, bytes);
            }
        }

        private void StartDecode(int openGeneration, Scene2GuideStageId stageId, string fileName, byte[] bytes)
        {
            cancellation = new CancellationTokenSource();
            BlockingCollection<DecodedFrame> frameQueue = new BlockingCollection<DecodedFrame>(
                new ConcurrentQueue<DecodedFrame>(),
                frameBufferCapacity);
            frames = frameQueue;
            CancellationToken token = cancellation.Token;

            decoderTask = Task.Run(
                () => DecodeLoop(openGeneration, stageId, fileName, bytes, frameQueue, token),
                token);
        }

        private void DecodeLoop(
            int openGeneration,
            Scene2GuideStageId stageId,
            string fileName,
            byte[] bytes,
            BlockingCollection<DecodedFrame> queue,
            CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && openGeneration == generation)
                {
                    int decodedCount = 0;
                    using (GifStream gif = new GifStream(bytes))
                    {
                        while (gif.HasMoreData && !token.IsCancellationRequested && openGeneration == generation)
                        {
                            if (gif.CurrentToken == GifStream.Token.Image)
                            {
                                ThreeDISevenZeroR.UnityGifDecoder.Model.GifImage image = gif.ReadImage();
                                Color32[] pixels = new Color32[image.colors.Length];
                                Array.Copy(image.colors, pixels, pixels.Length);
                                DecodedFrame frame = new DecodedFrame(
                                    gif.Header.width,
                                    gif.Header.height,
                                    pixels,
                                    (float)Math.Max(0.01, image.SafeDelaySeconds));
                                queue.Add(frame, token);
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
                lock (failureSync)
                {
                    pendingDecoderFailure = fileName + "：" + exception.Message;
                    pendingDecoderLog = $"GIF 解码失败（{stageId}/{fileName}）：{exception}";
                }
            }
        }

        private void Update()
        {
            string failure = null;
            lock (failureSync)
            {
                if (!string.IsNullOrEmpty(pendingDecoderFailure))
                {
                    failure = pendingDecoderFailure;
                    pendingDecoderFailure = null;
                }
            }

            if (!string.IsNullOrEmpty(failure))
            {
                cancellation?.Cancel();
                string decoderLog;
                lock (failureSync)
                    decoderLog = pendingDecoderLog;
                Debug.LogError("[Scene2RealtimeGuide] " + decoderLog, this);
                State = GuideGifLoadState.Failed;
                StateChanged?.Invoke(State, failure);
                return;
            }

            if (State == GuideGifLoadState.Failed || frames == null)
                return;

            if (currentFrame == null)
            {
                DecodedFrame first;
                if (frames.TryTake(out first))
                    ShowFrame(first);
                return;
            }

            frameTimer += Time.unscaledDeltaTime;
            if (frameTimer < currentFrame.delaySeconds)
                return;

            DecodedFrame next;
            if (!frames.TryTake(out next))
                return;

            frameTimer -= currentFrame.delaySeconds;
            ShowFrame(next);
        }

        private void ShowFrame(DecodedFrame frame)
        {
            if (frame == null || frame.pixels == null)
                return;

            if (texture == null || texture.width != frame.width || texture.height != frame.height)
            {
                if (texture != null)
                    Destroy(texture);
                texture = new Texture2D(frame.width, frame.height, TextureFormat.RGBA32, false, false)
                {
                    name = "Scene2GuideGifFrame",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            texture.SetPixels32(frame.pixels);
            texture.Apply(false, false);
            currentFrame = frame;
            if (State != GuideGifLoadState.Playing)
            {
                State = GuideGifLoadState.Playing;
                StateChanged?.Invoke(State, string.Empty);
            }
            FrameChanged?.Invoke(texture);
        }

        private void SetStateIfCurrent(int openGeneration, GuideGifLoadState state, string message)
        {
            if (openGeneration != generation)
                return;
            State = state;
            StateChanged?.Invoke(state, message ?? string.Empty);
        }

        private void OnDestroy()
        {
            Close();
        }

        private sealed class DecodedFrame
        {
            public readonly int width;
            public readonly int height;
            public readonly Color32[] pixels;
            public readonly float delaySeconds;

            public DecodedFrame(int frameWidth, int frameHeight, Color32[] framePixels, float delay)
            {
                width = frameWidth;
                height = frameHeight;
                pixels = framePixels;
                delaySeconds = delay;
            }
        }
    }
}
