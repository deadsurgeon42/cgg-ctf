using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TShockAPI;

namespace CGGCTF
{
  public class CtfTileSystem
  {
    public void RemoveBadStuffs()
    {
      for (var i = 0; i < MaxX; ++i)
      for (var j = 0; j < MaxY; ++j) // grass
        if (Main.tile[i, j].type == 23
            || Main.tile[i, j].type == 199)
          SetTile(i, j, 2);

        // stone
        else if (Main.tile[i, j].type == 25
                 || Main.tile[i, j].type == 203)
          SetTile(i, j, 1);

        // sand
        else if (Main.tile[i, j].type == 112
                 || Main.tile[i, j].type == 234)
          SetTile(i, j, 53);

        // sandstone
        else if (Main.tile[i, j].type == 400
                 || Main.tile[i, j].type == 401)
          SetTile(i, j, 396);

        // hardened sand
        else if (Main.tile[i, j].type == 398
                 || Main.tile[i, j].type == 399)
          SetTile(i, j, 397);

        // ice
        else if (Main.tile[i, j].type == 163
                 || Main.tile[i, j].type == 200)
          SetTile(i, j, 161);

        // life crystals, anvils, hellforge
        else if (Main.tile[i, j].type == 12
                 || Main.tile[i, j].type == 16
                 || Main.tile[i, j].type == 77)
          SetTile(i, j, -1);

        // plants
        else if (Main.tile[i, j].type == 24
                 || Main.tile[i, j].type == 201)
          SetTile(i, j, -1);

        // thorns
        else if (Main.tile[i, j].type == 32
                 || Main.tile[i, j].type == 352)
          SetTile(i, j, -1);

        // corruption walls
        else if (Main.tile[i, j].wall >= 188
                 && Main.tile[i, j].wall <= 191)
          SetWall(i, j, 196 + (Main.tile[i, j].wall - 188));

        // crimson walls
        else if (Main.tile[i, j].wall >= 192
                 && Main.tile[i, j].wall <= 195)
          SetWall(i, j, 196 + (Main.tile[i, j].wall - 192));

        // grass walls
        else if (Main.tile[i, j].wall == 69
                 || Main.tile[i, j].wall == 81)
          SetWall(i, j, 63);

        // stone walls
        else if (Main.tile[i, j].wall == 3
                 || Main.tile[i, j].wall == 83)
          SetWall(i, j, 1);

        // sand walls
        else if (Main.tile[i, j].wall == 217
                 || Main.tile[i, j].wall == 218)
          SetWall(i, j, 216);

        // hardened sand walls
        else if (Main.tile[i, j].wall == 220
                 || Main.tile[i, j].wall == 221)
          SetWall(i, j, 187);

        // pots
        else if (Main.tile[i, j].type == 28)
          SetTile(i, j, -1);

      // remove banned items from chests
      for (var i = 0; i < Main.chest.Length; ++i)
      {
        var chest = Main.chest[i];
        if (chest == null)
          continue;
        for (var j = 0; j < chest.item.Length; ++j)
        {
          var item = chest.item[j];
          if (TShock.Itembans.ItemIsBanned(item.Name)
              || item.type == ItemID.InvisibilityPotion)
            chest.item[j] = new Item();
        }
      }
    }

    #region Variables

    private int FlagDistance => CtfConfig.FlagDistance;
    private int SpawnDistance => CtfConfig.SpawnDistance;

    private int MaxX => Main.maxTilesX;

    private int MaxY => Main.maxTilesY;

    private int MapMiddle => MaxX / 2;

    private Point _redSpawn, _blueSpawn, _redFlag, _blueFlag;
    private Rectangle _redSpawnArea, _blueSpawnArea;
    private Rectangle _redFlagNoEdit, _blueFlagNoEdit;
    private Rectangle _redFlagArea, _blueFlagArea;
    private Tile[,] _realTiles;

    public Point RedSpawn => new Point(_redSpawn.X, _redSpawn.Y - 3);

    public Point BlueSpawn => new Point(_blueSpawn.X, _blueSpawn.Y - 3);

    public CtfTeam LeftTeam => _redSpawn.X < _blueSpawn.X ? CtfTeam.Red : CtfTeam.Blue;

    public CtfTeam RightTeam => _redSpawn.X > _blueSpawn.X ? CtfTeam.Red : CtfTeam.Blue;

    private int WallWidth => CtfConfig.WallWidth;

    private int WallMiddle => MaxX / 2;

    private int WallLeft => WallMiddle - WallWidth;

    private int WallRight => WallMiddle + WallWidth;

    // TODO - name capitalization
    public ushort RedBlock = TileID.RedBrick;
    public ushort BlueBlock = TileID.CobaltBrick;
    public ushort RedWall = WallID.RedBrick;
    public ushort BlueWall = WallID.CobaltBrick;
    public ushort GrayBlock = TileID.GrayBrick;
    public ushort MiddleBlock = TileID.LihzahrdBrick;
    public ushort FlagTile = TileID.Banners;
    public ushort FlagRedStyle = 0;
    public ushort FlagBlueStyle = 2;

    #endregion

    #region Code from WorldEdit

    public bool IsSolidTile(int x, int y)
    {
      return x < 0 || y < 0 || x >= MaxX || y >= Main.maxTilesY ||
             Main.tile[x, y].active() && Main.tileSolid[Main.tile[x, y].type];
    }

    public void SetTile(int i, int j, int tileType, int style = 0)
    {
      var tile = Main.tile[i, j];
      switch (tileType)
      {
        case -1:
          tile.active(false);
          tile.frameX = -1;
          tile.frameY = -1;
          tile.liquidType(0);
          tile.liquid = 0;
          tile.type = 0;
          return;
        case -2:
          tile.active(false);
          tile.liquidType(1);
          tile.liquid = 255;
          tile.type = 0;
          return;
        case -3:
          tile.active(false);
          tile.liquidType(2);
          tile.liquid = 255;
          tile.type = 0;
          return;
        case -4:
          tile.active(false);
          tile.liquidType(0);
          tile.liquid = 255;
          tile.type = 0;
          return;
        default:
          if (Main.tileFrameImportant[tileType])
          {
            WorldGen.PlaceTile(i, j, tileType, false, false, -1, style);
          }
          else
          {
            tile.active(true);
            tile.frameX = -1;
            tile.frameY = -1;
            tile.liquidType(0);
            tile.liquid = 0;
            tile.slope(0);
            tile.color(0);
            tile.type = (ushort) tileType;
          }

          return;
      }
    }

    public void SetWall(int i, int j, int wallType)
    {
      Main.tile[i, j].wall = (byte) wallType;
    }

    public void ResetSection(int x, int x2, int y, int y2)
    {
      var lowX = Netplay.GetSectionX(x);
      var highX = Netplay.GetSectionX(x2);
      var lowY = Netplay.GetSectionY(y);
      var highY = Netplay.GetSectionY(y2);
      foreach (var sock in Netplay.Clients.Where(s => s.IsActive))
        for (var i = lowX; i <= highX; i++)
        for (var j = lowY; j <= highY; j++)
          sock.TileSections[i, j] = false;
    }

    #endregion

    #region Positions

    public int FindGround(int x)
    {
      var y = 0;
      for (var i = 1; i < Main.maxTilesY; ++i)
        if (Main.tile[x, i].type == TileID.Cloud
            || Main.tile[x, i].type == TileID.RainCloud)
          y = 0;
        else if (IsSolidTile(x, i) && y == 0)
          y = i;
      y -= 2;
      return y;
    }

    public void DecidePositions()
    {
      var f1X = MapMiddle - FlagDistance;
      var f1Y = FindGround(f1X) - 1;

      var f2X = MapMiddle + FlagDistance;
      var f2Y = FindGround(f2X) - 1;

      var s1X = MapMiddle - SpawnDistance;
      var s1Y = FindGround(s1X) - 2;

      var s2X = MapMiddle + SpawnDistance;
      var s2Y = FindGround(s2X) - 2;

      if (CtfUtils.Random(2) == 0)
      {
        _redFlag.X = f1X;
        _redFlag.Y = f1Y;
        _redSpawn.X = s1X;
        _redSpawn.Y = s1Y;
        _blueFlag.X = f2X;
        _blueFlag.Y = f2Y;
        _blueSpawn.X = s2X;
        _blueSpawn.Y = s2Y;
      }
      else
      {
        _redFlag.X = f2X;
        _redFlag.Y = f2Y;
        _redSpawn.X = s2X;
        _redSpawn.Y = s2Y;
        _blueFlag.X = f1X;
        _blueFlag.Y = f1Y;
        _blueSpawn.X = s1X;
        _blueSpawn.Y = s1Y;
      }
    }

    #endregion

    #region Middle block

    public void AddMiddleBlock()
    {
      _realTiles = new Tile[WallWidth * 2 + 1, Main.maxTilesY];

      for (var x = 0; x <= 2 * WallWidth; ++x)
      for (var y = 0; y < Main.maxTilesY; ++y)
      {
        _realTiles[x, y] = new Tile(Main.tile[WallLeft + x, y]);
        SetTile(WallLeft + x, y, MiddleBlock);
      }

      ResetSection(WallLeft, WallRight, 0, Main.maxTilesY);
    }

    public void RemoveMiddleBlock()
    {
      for (var x = 0; x <= 2 * WallWidth; ++x)
      for (var y = 0; y < Main.maxTilesY; ++y)
        Main.tile[WallLeft + x, y] = _realTiles[x, y];

      ResetSection(WallLeft, WallRight, 0, Main.maxTilesY);
      _realTiles = null;
    }

    #endregion

    #region Spawns

    public void AddSpawns()
    {
      AddLeftSpawn();
      AddRightSpawn();
    }

    public void AddLeftSpawn()
    {
      Point leftSpawn;
      ushort tileId;
      ushort wallId;

      if (LeftTeam == CtfTeam.Red)
      {
        leftSpawn = _redSpawn;
        tileId = RedBlock;
        wallId = RedWall;
        _redSpawnArea = new Rectangle(leftSpawn.X - 6, leftSpawn.Y - 9, 13 + 1, 11 + 1);
      }
      else
      {
        leftSpawn = _blueSpawn;
        tileId = BlueBlock;
        wallId = BlueWall;
        _blueSpawnArea = new Rectangle(leftSpawn.X - 6, leftSpawn.Y - 9, 13 + 1, 11 + 1);
      }

      for (var i = -6; i <= 7; ++i)
      for (var j = -9; j <= 2; ++j)
      {
        SetTile(leftSpawn.X + i, leftSpawn.Y + j, -1);
        SetWall(leftSpawn.X + i, leftSpawn.Y + j, 0);
      }

      for (var i = -5; i <= 6; ++i)
        SetTile(leftSpawn.X + i, leftSpawn.Y + 1, tileId);
      for (var i = -4; i <= 5; ++i)
        SetWall(leftSpawn.X + i, leftSpawn.Y - 5, wallId);
      for (var i = 1; i <= 3; ++i)
      for (var j = 0; j < i; ++j)
        SetWall(leftSpawn.X + 2 + j, leftSpawn.Y - 9 + i, wallId);
      for (var i = 3; i >= 1; --i)
      for (var j = 0; j < i; ++j)
        SetWall(leftSpawn.X + 2 + j, leftSpawn.Y - 1 - i, wallId);

      ResetSection(leftSpawn.X - 6, leftSpawn.X + 7, leftSpawn.Y - 9, leftSpawn.Y + 2);
    }

    public void AddRightSpawn()
    {
      Point rightSpawn;
      ushort tileId;
      ushort wallId;

      if (RightTeam == CtfTeam.Blue)
      {
        rightSpawn = _blueSpawn;
        tileId = BlueBlock;
        wallId = BlueWall;
        _blueSpawnArea = new Rectangle(rightSpawn.X - 7, rightSpawn.Y - 9, 13 + 1, 11 + 1);
      }
      else
      {
        rightSpawn = _redSpawn;
        tileId = RedBlock;
        wallId = RedWall;
        _redSpawnArea = new Rectangle(rightSpawn.X - 7, rightSpawn.Y - 9, 13 + 1, 11 + 1);
      }

      for (var i = -7; i <= 6; ++i)
      for (var j = -9; j <= 2; ++j)
      {
        SetTile(rightSpawn.X + i, rightSpawn.Y + j, -1);
        SetWall(rightSpawn.X + i, rightSpawn.Y + j, 0);
      }

      for (var i = -6; i <= 5; ++i)
        SetTile(rightSpawn.X + i, rightSpawn.Y + 1, tileId);
      for (var i = -5; i <= 4; ++i)
        SetWall(rightSpawn.X + i, rightSpawn.Y - 5, wallId);
      for (var i = 1; i <= 3; ++i)
      for (var j = 0; j < i; ++j)
        SetWall(rightSpawn.X - 2 - j, rightSpawn.Y - 9 + i, wallId);
      for (var i = 3; i >= 1; --i)
      for (var j = 0; j < i; ++j)
        SetWall(rightSpawn.X - 2 - j, rightSpawn.Y - 1 - i, wallId);

      ResetSection(rightSpawn.X - 7, rightSpawn.X + 6, rightSpawn.Y - 9, rightSpawn.Y + 2);
    }

    #endregion

    #region Flags

    public void AddFlags()
    {
      AddRedFlag(true);
      AddBlueFlag(true);
    }

    public void AddRedFlag(bool full = false)
    {
      var redTile = RedBlock;

      if (full)
      {
        _redFlagArea = new Rectangle(_redFlag.X - 1, _redFlag.Y - 4, 3 + 1, 2 + 1);
        _redFlagNoEdit = new Rectangle(_redFlag.X - 3, _redFlag.Y - 6, 6 + 1, 7 + 1);
        for (var i = -3; i <= 3; ++i)
        for (var j = -6; j <= 1; ++j)
          SetTile(_redFlag.X + i, _redFlag.Y + j, -1);
      }

      for (var i = -1; i <= 1; ++i)
      {
        SetTile(_redFlag.X + i, _redFlag.Y, redTile);
        SetTile(_redFlag.X + i, _redFlag.Y - 5, redTile);
        SetTile(_redFlag.X + i, _redFlag.Y - 4, FlagTile, FlagRedStyle);
      }

      ResetSection(_redFlag.X - 3, _redFlag.X + 3, _redFlag.Y - 6, _redFlag.Y + 1);
    }

    public void AddBlueFlag(bool full = false)
    {
      var flagTile = TileID.Banners;
      var blueTile = BlueBlock;

      if (full)
      {
        _blueFlagArea = new Rectangle(_blueFlag.X - 1, _blueFlag.Y - 4, 3 + 1, 2 + 1);
        _blueFlagNoEdit = new Rectangle(_blueFlag.X - 3, _blueFlag.Y - 6, 6 + 1, 7 + 1);
        for (var i = -3; i <= 3; ++i)
        for (var j = -6; j <= 1; ++j)
          SetTile(_blueFlag.X + i, _blueFlag.Y + j, -1);
      }

      for (var i = -1; i <= 1; ++i)
      {
        SetTile(_blueFlag.X + i, _blueFlag.Y, blueTile);
        SetTile(_blueFlag.X + i, _blueFlag.Y - 5, blueTile);
        SetTile(_blueFlag.X + i, _blueFlag.Y - 4, flagTile, FlagBlueStyle);
      }

      ResetSection(_blueFlag.X - 3, _blueFlag.X + 3, _blueFlag.Y - 6, _blueFlag.Y + 1);
    }

    public void RemoveRedFlag()
    {
      for (var i = -1; i <= 1; ++i)
      for (var j = 4; j >= 2; --j)
        SetTile(_redFlag.X + i, _redFlag.Y - j, -1);
      ResetSection(_redFlag.X - 3, _redFlag.X + 3, _redFlag.Y - 6, _redFlag.Y + 1);
    }

    public void RemoveBlueFlag()
    {
      for (var i = -1; i <= 1; ++i)
      for (var j = 4; j >= 2; --j)
        SetTile(_blueFlag.X + i, _blueFlag.Y - j, -1);
      ResetSection(_blueFlag.X - 3, _blueFlag.X + 3, _blueFlag.Y - 6, _blueFlag.Y + 1);
    }

    #endregion

    #region Check functions

    public bool InRedSide(int x)
    {
      if (LeftTeam == CtfTeam.Red)
        return x < WallMiddle;
      return x > WallMiddle;
    }

    public bool InBlueSide(int x)
    {
      if (LeftTeam == CtfTeam.Blue)
        return x < WallMiddle;
      return x > WallMiddle;
    }

    public bool InRedFlag(int x, int y)
    {
      return _redFlagArea.Contains(x, y);
    }

    public bool InBlueFlag(int x, int y)
    {
      return _blueFlagArea.Contains(x, y);
    }

    public bool InvalidPlace(CtfTeam team, int x, int y, bool middle)
    {
      if (middle && x >= WallLeft - 1 && x <= WallRight + 1
          || _redSpawnArea.Contains(x, y)
          || _blueSpawnArea.Contains(x, y)
          || x >= _redFlagNoEdit.Left && x < _redFlagNoEdit.Right && y < _redFlagNoEdit.Bottom
          || x >= _blueFlagNoEdit.Left && x < _blueFlagNoEdit.Right && y < _blueFlagNoEdit.Bottom
          || team == CtfTeam.Red && Main.tile[x, y].type == TileID.CobaltBrick
          || team == CtfTeam.Blue && Main.tile[x, y].type == TileID.RedBrick)
        return true;

      return false;
    }

    #endregion
  }
}