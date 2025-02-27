using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UniVRM10;

namespace AniLipSync.VRM
{
    public class AnimMorphTarget : MonoBehaviour
    {
        [Tooltip("aa, E, ih, oh, ou のそれぞれの音素へ遷移する際に、BlendShapeの重みを時間をかけて変化させるためのカーブ")]
        public AnimationCurve[] transitionCurves = new AnimationCurve[5];

        [Tooltip("カーブの値をBlendShapeに適用する際の倍率")]
        public float curveAmplifier = 1.0f;

        [Range(0.0f, 100.0f), Tooltip("この閾値未満の音素の重みは無視する")]
        public float weightThreashold = 2.0f;

        [Tooltip("BlendShapeの重みを変化させるフレームレート")]
        public float frameRate = 12.0f;

        [Tooltip("BlendShapeの値を変化させるSkinnedMeshRenderer")]
        public Vrm10Instance Vrm10Instance;

        [Tooltip("aa, E, ih, oh, ouの順で割り当てるBlendShapeのindex")]
        private Dictionary<ExpressionKey, float> defaultValueDictionary = new Dictionary<ExpressionKey, float> {
            {ExpressionKey.Aa, 0.0f },
            {ExpressionKey.Ee, 0.0f },
            {ExpressionKey.Ih, 0.0f },
            {ExpressionKey.Oh, 0.0f },
            {ExpressionKey.Ou, 0.0f },
        };
        private List<ExpressionKey> keyList;

        [Tooltip("OVRLipSyncに渡すSmoothing amountの値")]
        public int smoothAmount = 100;

        OVRLipSyncContextBase context;
        OVRLipSync.Viseme previousViseme = OVRLipSync.Viseme.sil;
        float transitionTimer = 0.0f;
        float frameRateTimer = 0.0f;



        void Start()
        {
            keyList = new List<ExpressionKey>(defaultValueDictionary.Keys);

            if (Vrm10Instance == null)
            {
                Debug.LogError("Vrm10instanceが指定されていません。", this);
            }

            context = GetComponent<OVRLipSyncContextBase>();
            if (context == null)
            {
                Debug.LogError("同じGameObjectにOVRLipSyncContextBaseを継承したクラスが見つかりません。", this);
            }

            context.Smoothing = smoothAmount;
        }

        void Update()
        {
            if (context == null || Vrm10Instance == null)
            {
                return;
            }

            var frame = context.GetCurrentPhonemeFrame();
            if (frame == null)
            {
                return;
            }

            transitionTimer += Time.deltaTime;

            // 設定したフレームレートへUpdate関数を低下させる
            frameRateTimer += Time.deltaTime;
            if (frameRateTimer < 1.0f / frameRate)
            {
                return;
            }
            frameRateTimer -= 1.0f / frameRate;

            // すでに設定されているBlendShapeの重みをリセット
            foreach (var key in keyList)
            {
                defaultValueDictionary[key] = 0.0f;
            }

            // 最大の重みを持つ音素を探す
            var maxVisemeIndex = 0;
            var maxVisemeWeight = 0.0f;
            // 子音は無視する
            for (var i = (int)OVRLipSync.Viseme.aa; i < frame.Visemes.Length; i++)
            {
                if (frame.Visemes[i] > maxVisemeWeight)
                {
                    maxVisemeWeight = frame.Visemes[i];
                    maxVisemeIndex = i;
                }
            }

            // 音素の重みが小さすぎる場合は口を閉じる
            if (maxVisemeWeight * 100.0f < weightThreashold)
            {
                transitionTimer = 0.0f;
            }

            // 音素の切り替わりでタイマーをリセットする
            if (previousViseme != (OVRLipSync.Viseme)maxVisemeIndex)
            {
                transitionTimer = 0.0f;
                previousViseme = (OVRLipSync.Viseme)maxVisemeIndex;
            }
            var visemeIndex = maxVisemeIndex - (int)OVRLipSync.Viseme.aa;
            if (visemeIndex >= 0)
            {
                defaultValueDictionary[keyList[visemeIndex]] = transitionCurves[visemeIndex].Evaluate(transitionTimer) * curveAmplifier;
            }
            Vrm10Instance.Runtime.Expression.SetWeights(defaultValueDictionary.ToList());
        }
    }
}
