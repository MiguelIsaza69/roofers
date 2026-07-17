using UnityEngine;

namespace RoofingSimulator.Player
{
    /// <summary>
    /// PEAK-style stamina that doubles as the player's health. There is no death:
    /// stamina powers sprinting, jumping, hanging and ledge-saves, and when it runs
    /// out those actions are simply unavailable until it regenerates.
    ///
    /// Falls and hazards add "injury" which lowers the *effective maximum* (the bar
    /// shrinks, like PEAK), so repeated punishment leaves less and less to work with.
    /// Injury recovers slowly over time (or instantly via a future rest/coffee item).
    /// A small reserve is always kept so the player is never fully locked out.
    /// </summary>
    public class PlayerStamina : MonoBehaviour
    {
        [Header("Capacity")]
        [SerializeField] private float maxStamina = 100f;
        [Tooltip("Stamina the player can never drop below (so they're never hard-locked).")]
        [SerializeField] private float reserve = 8f;

        [Header("Regeneration")]
        [Tooltip("Stamina restored per second when not spending it.")]
        [SerializeField] private float regenPerSecond = 22f;
        [Tooltip("Seconds after spending stamina before regen kicks in.")]
        [SerializeField] private float regenDelay = 0.6f;

        [Header("Injury (shrinks the bar, PEAK-style)")]
        [Tooltip("How much of the bar a long fall can remove, at most.")]
        [SerializeField] private float maxInjury = 60f;
        [Tooltip("Injury healed back per second (slow).")]
        [SerializeField] private float injuryHealPerSecond = 1.5f;

        private float current;
        private float injury;          // amount subtracted from the effective max
        private float regenBlockedFor;  // countdown before regen resumes
        private float regenScale = 1f;  // weather (Fase C4): cold slows regeneration
        private float costScale = 1f;   // weather (Fase C4): heat makes every effort cost more
        private float coffeeUntil;      // consumable (Fase E2): regen boost window

        /// <summary>True while a coffee boost is running (HUD/consumable belt).</summary>
        public bool CoffeeActive => Time.time < coffeeUntil;

        /// <summary>Current usable stamina.</summary>
        public float Current => current;
        /// <summary>Effective max after injury (the visible bar's top).</summary>
        public float EffectiveMax => Mathf.Max(reserve + 1f, maxStamina - injury);
        /// <summary>0..1 fill against the (injured) effective max — for the HUD.</summary>
        public float Normalized => Mathf.Clamp01(current / Mathf.Max(1f, EffectiveMax));
        /// <summary>0..1 of how injured the player is (red zone on the bar).</summary>
        public float InjuryNormalized => Mathf.Clamp01(injury / maxStamina);
        public bool IsExhausted => current <= reserve + 0.01f;

        private void Awake()
        {
            current = maxStamina;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Injury slowly heals so the bar grows back to full over time.
            if (injury > 0f)
            {
                injury = Mathf.Max(0f, injury - injuryHealPerSecond * dt);
            }

            // Regenerate once the spend cooldown has elapsed.
            if (regenBlockedFor > 0f)
            {
                regenBlockedFor -= dt;
            }
            else if (current < EffectiveMax)
            {
                float boost = CoffeeActive ? 1.5f : 1f; // coffee (Fase E2) beats the cold a while
                current = Mathf.Min(EffectiveMax, current + regenPerSecond * regenScale * boost * dt);
            }

            // Injury can pull the effective max below the current value.
            if (current > EffectiveMax)
            {
                current = EffectiveMax;
            }
        }

        /// <summary>
        /// Try to spend <paramref name="amount"/> in one go (jump, ledge-save).
        /// Returns false and spends nothing if there isn't enough above the reserve.
        /// </summary>
        public bool TrySpend(float amount)
        {
            amount *= costScale; // heat makes the same jump cost more
            if (current - amount < reserve)
            {
                return false;
            }
            current -= amount;
            regenBlockedFor = regenDelay;
            return true;
        }

        /// <summary>
        /// Continuously drain (sprinting, hanging, carrying). Stops at the reserve and
        /// returns true while there was stamina to spend, false once exhausted.
        /// </summary>
        public bool Drain(float perSecond)
        {
            if (IsExhausted)
            {
                return false;
            }
            current = Mathf.Max(reserve, current - perSecond * costScale * Time.deltaTime);
            regenBlockedFor = regenDelay;
            return true;
        }

        /// <summary>Weather hook (Fase C4): cold scales regen down (0..1), heat scales every
        /// cost up (≥1). The thermal clothing / water upgrades of Fase E will soften these.</summary>
        public void SetClimate(float regenScale01, float costScaleUp)
        {
            regenScale = Mathf.Clamp(regenScale01, 0.1f, 1f);
            costScale = Mathf.Max(1f, costScaleUp);
        }

        /// <summary>Add injury (a fall, a hazard): shrinks the bar and drops current with it.</summary>
        public void AddInjury(float amount)
        {
            injury = Mathf.Clamp(injury + amount, 0f, maxInjury);
            if (current > EffectiveMax)
            {
                current = EffectiveMax;
            }
            regenBlockedFor = regenDelay;
        }

        /// <summary>
        /// Coffee (Fase E2): instant top-up, a nip of injury healed, and regen ×1.5 for
        /// <paramref name="seconds"/> — the promised "rest/coffee item".
        /// </summary>
        public void DrinkCoffee(float seconds)
        {
            injury = Mathf.Max(0f, injury - 8f);
            current = Mathf.Min(EffectiveMax, current + 35f);
            coffeeUntil = Time.time + Mathf.Max(coffeeUntil - Time.time, 0f) + seconds; // stacks politely
        }

        /// <summary>Full top-up (e.g. respawn at the supply area or a rest item).</summary>
        public void Refill(bool clearInjury = false)
        {
            if (clearInjury)
            {
                injury = 0f;
            }
            current = EffectiveMax;
        }
    }
}
