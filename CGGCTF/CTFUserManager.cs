using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace CGGCTF
{
  public class CtfUserManager
  {
    // database initialization
    private readonly IDbConnection _db;

    public CtfUserManager()
    {
      switch (TShock.Config.StorageType.ToLower())
      {
        case "mysql":
          var host = TShock.Config.MySqlHost.Split(':');
          _db = new MySqlConnection
          {
            ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
              host[0],
              host.Length == 1 ? "3306" : host[1],
              TShock.Config.MySqlDbName,
              TShock.Config.MySqlUsername,
              TShock.Config.MySqlPassword)
          };
          break;
        case "sqlite":
          var dbPath = Path.Combine(TShock.SavePath, "cggctf.sqlite");
          _db = new SqliteConnection(string.Format("uri=file://{0},Version=3", dbPath));
          break;
      }

      var creator = new SqlTableCreator(_db,
        _db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder) new SqliteQueryCreator() : new MysqlQueryCreator());
      creator.EnsureTableStructure(new SqlTable("ctfusers",
        new SqlColumn("ID", MySqlDbType.Int32) {Primary = true},
        new SqlColumn("Coins", MySqlDbType.Int32),
        new SqlColumn("Kills", MySqlDbType.Int32),
        new SqlColumn("Deaths", MySqlDbType.Int32),
        new SqlColumn("Assists", MySqlDbType.Int32),
        new SqlColumn("Wins", MySqlDbType.Int32),
        new SqlColumn("Loses", MySqlDbType.Int32),
        new SqlColumn("Draws", MySqlDbType.Int32),
        new SqlColumn("Classes", MySqlDbType.Text)));
    }

    public CtfUser GetUser(int id)
    {
      try
      {
        using (var reader = _db.QueryReader("SELECT * FROM ctfusers WHERE ID = @0", id))
        {
          if (reader.Read())
            return new CtfUser
            {
              Id = reader.Get<int>("ID"),
              Coins = reader.Get<int>("Coins"),
              Kills = reader.Get<int>("Kills"),
              Deaths = reader.Get<int>("Deaths"),
              Assists = reader.Get<int>("Assists"),
              Wins = reader.Get<int>("Wins"),
              Loses = reader.Get<int>("Loses"),
              Draws = reader.Get<int>("Draws"),
              Classes = ParseClasses(reader.Get<string>("Classes"))
            };

          var ret = new CtfUser();
          ret.Id = id;
          if (_db.Query("INSERT INTO ctfusers (ID, Coins, Kills, " +
                       "Deaths, Assists, Wins, Loses, Draws, Classes) " +
                       "VALUES (@0, @1, @2, @3, @4, @5, @6, @7, @8)",
                ret.Id, ret.Coins, ret.Kills, ret.Deaths,
                ret.Assists, ret.Wins, ret.Loses, ret.Draws,
                ClassesToString(ret.Classes)) != 0)
            return ret;
        }
      }
      catch (Exception ex)
      {
        TShock.Log.Error(ex.ToString());
      }

      return null;
    }

    public List<CtfUser> GetUsers()
    {
      var list = new List<CtfUser>();
      try
      {
        using (var reader = _db.QueryReader("SELECT * FROM ctfusers"))
        {
          while (reader.Read())
            list.Add(new CtfUser
            {
              Id = reader.Get<int>("ID"),
              Coins = reader.Get<int>("Coins"),
              Kills = reader.Get<int>("Kills"),
              Deaths = reader.Get<int>("Deaths"),
              Assists = reader.Get<int>("Assists"),
              Wins = reader.Get<int>("Wins"),
              Loses = reader.Get<int>("Loses"),
              Draws = reader.Get<int>("Draws"),
              Classes = ParseClasses(reader.Get<string>("Classes"))
            });
        }
      }
      catch (Exception ex)
      {
        TShock.Log.Error(ex.ToString());
      }

      return list;
    }

    public bool SaveUser(CtfUser user)
    {
      try
      {
        _db.Query("UPDATE ctfusers SET Coins = @0, Kills = @1, Deaths = @2, " +
                 "Assists = @3, Wins = @4, Loses = @5, Draws = @6, Classes = @7 WHERE ID = @8",
          user.Coins, user.Kills, user.Deaths, user.Assists,
          user.Wins, user.Loses, user.Draws, ClassesToString(user.Classes), user.Id);
        return true;
      }
      catch (Exception ex)
      {
        TShock.Log.Error(ex.ToString());
      }

      return false;
    }

    private List<int> ParseClasses(string classes)
    {
      var ret = new List<int>();
      if (string.IsNullOrWhiteSpace(classes))
        return ret;
      var list = classes.Split(',');
      foreach (var cls in list) ret.Add(int.Parse(cls));
      return ret;
    }

    private string ClassesToString(List<int> classes)
    {
      return string.Join(",", classes);
    }
  }
}