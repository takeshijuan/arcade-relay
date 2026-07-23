// EconomySystem.cs — ラン内経済（純粋 C#・エンジン非依存。S-05）。
// gdd「資金（ラン内経済）」節（P-01）。残高の唯一の保持者。負値禁止のガードを持つ
// （TrySpend は残高不足なら変化なしで false を返す。マイナス残高には絶対にならない）。
// MonoBehaviour/シーン API は使わない（rules/unity-code.md 規約3）。Mathf は値型として使用可。
using System;
using UnityEngine;

namespace ForgeGame.Systems
{
    public sealed class EconomySystem
    {
        public int Gold { get; private set; }

        public EconomySystem(int startingGold)
        {
            if (startingGold < 0) throw new ArgumentOutOfRangeException(nameof(startingGold), "startingGold は 0 以上である必要がある。");
            Gold = startingGold;
        }

        /// <summary>資金が足りる場合のみ減算する（不足時は残高を変えず false を返す）。</summary>
        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "amount は 0 以上である必要がある。");
            if (Gold < amount) return false;
            Gold -= amount;
            return true;
        }

        /// <summary>撃破報酬・ウェーブクリア報酬・売却返還等の加算（負値禁止）。</summary>
        public void Add(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "amount は 0 以上である必要がある。");
            Gold += amount;
        }

        /// <summary>
        /// 基礎コストへ割引率（UPG-02。ラン間アップグレード購入の反映は S-14 で実装済み — 呼び出し元
        /// Components/BuildSpotController が Systems/Meta/MetaProgression.ComputeTowerDiscountRate で
        /// 算出した値を渡す。未購入=Lv0 時は 0f）を乗算した実効コストを算出する。
        /// gdd「ビルドスポット選択・設置」「タワーアップグレード」節の実効コスト計算式に一致。
        /// </summary>
        public static int ComputeEffectiveCost(int baseCost, float discountRate)
        {
            float discounted = baseCost * (1f - discountRate);
            return Mathf.Max(0, Mathf.RoundToInt(discounted));
        }

        /// <summary>
        /// 売却返還額 = 投入額(investedGold) × TOWER_SELL_REFUND_RATE を最も近い整数に丸めた値（S-11）。
        /// gdd「売却」節・acceptance「投入額 N のタワーを売却すると資金が round(N×0.5) 増える」に一致する
        /// 丸め方式（ComputeEffectiveCost と同じ Mathf.RoundToInt + 非負ガード）。
        /// CR-CODE S-11 iter1 minor指摘への対応: 丸め方式は Unity の <c>Mathf.RoundToInt</c> が採用する
        /// banker's rounding（.5 は偶数側へ丸め。例: 42.5→42, 43.5→44）を意図的な既定として確定する
        /// （half-up ではない）。現行 GameConfig の投入額合計（75/90/135/160 等）は .5 タイでも偶数丸めに
        /// 収まるため既存 acceptance・テストと乖離しないが、UPG-02 割引（S-14）やチューニングでの偶数床タイ
        /// （例: N=85→42.5→42。half-up なら43）到達時はこの banker's rounding が正として扱われる。
        /// half-up が必要になった場合は本メソッドを <c>(int)Math.Round(refunded, MidpointRounding.AwayFromZero)</c>
        /// へ変更し、この注記も更新すること。
        /// </summary>
        public static int ComputeSellRefund(int investedGold, float refundRate)
        {
            if (investedGold < 0) throw new ArgumentOutOfRangeException(nameof(investedGold), "investedGold は 0 以上である必要がある。");
            float refunded = investedGold * refundRate;
            return Mathf.Max(0, Mathf.RoundToInt(refunded));
        }
    }
}
