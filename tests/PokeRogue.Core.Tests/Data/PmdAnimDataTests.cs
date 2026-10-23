using System.Numerics;
using System.Xml.Linq;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;

namespace PokeRogue.Core.Tests.Data;

public class PmdAnimDataTests
{
    private const string Xml = """
        <AnimData>
          <ShadowSize>2</ShadowSize>
          <Anims>
            <Anim>
              <Name>Walk</Name><Index>0</Index><FrameWidth>32</FrameWidth><FrameHeight>40</FrameHeight>
              <Durations><Duration>8</Duration><Duration>10</Duration><Duration>8</Duration></Durations>
            </Anim>
            <Anim>
              <Name>Attack</Name><Index>1</Index><FrameWidth>64</FrameWidth><FrameHeight>64</FrameHeight>
              <RushFrame>1</RushFrame><HitFrame>2</HitFrame><ReturnFrame>3</ReturnFrame>
              <Durations><Duration>2</Duration><Duration>2</Duration><Duration>4</Duration><Duration>2</Duration></Durations>
            </Anim>
            <Anim><Name>Laying</Name><CopyOf>Walk</CopyOf></Anim>
            <Anim><Name>Sleep</Name><CopyOf>Laying</CopyOf></Anim>
          </Anims>
        </AnimData>
        """;

    [Fact]
    public void Parses_frames_durations_and_attack_markers()
    {
        var data = PmdAnimData.Parse(XDocument.Parse(Xml));

        Assert.Equal(2, data.ShadowSize);
        var walk = data.Anims["Walk"];
        Assert.Equal((32, 40), (walk.FrameWidth, walk.FrameHeight));
        Assert.Equal([8, 10, 8], walk.Durations);
        Assert.Equal(26, walk.TotalTicks);

        var attack = data.Anims["Attack"];
        Assert.Equal((1, 2, 3), (attack.RushFrame, attack.HitFrame, attack.ReturnFrame));
    }

    [Fact]
    public void Resolves_copy_of_chains_to_the_real_sheet()
    {
        var sleep = PmdAnimData.Parse(XDocument.Parse(Xml)).Anims["Sleep"];

        Assert.Equal("Sleep", sleep.Name);
        Assert.Equal("Walk", sleep.SheetName);
        Assert.Equal(32, sleep.FrameWidth);
    }

    [Theory]
    [InlineData(0, 1, Direction8.Down)]
    [InlineData(1, 1, Direction8.DownRight)]
    [InlineData(1, 0, Direction8.Right)]
    [InlineData(1, -1, Direction8.UpRight)]
    [InlineData(0, -1, Direction8.Up)]
    [InlineData(-1, -1, Direction8.UpLeft)]
    [InlineData(-1, 0, Direction8.Left)]
    [InlineData(-1, 1, Direction8.DownLeft)]
    public void Direction_from_vector_matches_sheet_rows(float x, float y, Direction8 expected)
    {
        Assert.Equal(expected, Direction8Extensions.FromVector(new Vector2(x, y)));
        Assert.Equal(expected, Direction8Extensions.FromVector(expected.ToVector()));
    }
}
