using PokeRogue.Core.Data;

namespace PokeRogue.Game.Graphics;

/// <summary>Plays a PMD animation using its per-frame durations (in 60 Hz ticks).</summary>
public struct SpriteAnimator
{
    private int _tick;

    public PmdAnim? Anim { get; private set; }

    public int Frame { get; private set; }

    public bool Loop { get; private set; }

    public bool Finished { get; private set; }

    public void Play(PmdAnim anim, bool loop = true, bool restart = false)
    {
        if (!restart && ReferenceEquals(Anim, anim))
            return;

        Anim = anim;
        Loop = loop;
        Frame = 0;
        _tick = 0;
        Finished = false;
    }

    public void Update(int ticks = 1)
    {
        if (Anim is null || Finished)
            return;

        _tick += ticks;
        while (_tick >= Anim.Durations[Frame])
        {
            _tick -= Anim.Durations[Frame];
            if (Frame + 1 < Anim.Durations.Count)
            {
                Frame++;
            }
            else if (Loop)
            {
                Frame = 0;
            }
            else
            {
                Finished = true;
                _tick = 0;
                return;
            }
        }
    }
}
