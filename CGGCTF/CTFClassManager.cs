using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace CGGCTF
{
  public class CtfClassManager
  {
    // database initialization
    private readonly IDbConnection _db;

    public CtfClassManager()
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
      creator.EnsureTableStructure(new SqlTable("ctfclasses",
        new SqlColumn("ID", MySqlDbType.Int32) {Primary = true, AutoIncrement = true},
        new SqlColumn("Name", MySqlDbType.String) {Unique = true},
        new SqlColumn("Description", MySqlDbType.Text),
        new SqlColumn("HP", MySqlDbType.Int32),
        new SqlColumn("Mana", MySqlDbType.Int32),
        new SqlColumn("Inventory", MySqlDbType.Text),
        new SqlColumn("Price", MySqlDbType.Int32),
        new SqlColumn("Hidden", MySqlDbType.Int32),
        new SqlColumn("Sell", MySqlDbType.Int32)));
    }

    public List<CtfClass> GetClasses()
    {
      var classes = new List<CtfClass>();
      try
      {
        using (var reader = _db.QueryReader("SELECT * FROM ctfclasses"))
        {
          while (reader.Read())
            classes.Add(new CtfClass
            {
              Id = reader.Get<int>("ID"),
              Name = reader.Get<string>("Name"),
              Description = reader.Get<string>("Description"),
              Hp = reader.Get<int>("HP"),
              Mana = reader.Get<int>("Mana"),
              Inventory = reader.Get<string>("Inventory").Split('~').Select(NetItem.Parse).ToArray(),
              Price = reader.Get<int>("Price"),
              Hidden = reader.Get<int>("Hidden") != 0,
              Sell = reader.Get<int>("Sell") != 0
            });
        }
      }
      catch (Exception ex)
      {
        TShock.Log.Error(ex.ToString());
      }

      return classes;
    }

    public CtfClass GetClass(string name, bool caseSensitive = false)
    {
      var classes = GetClasses();
      if (caseSensitive)
        return classes.FirstOrDefault(cls => cls.Name == name);
      return classes.FirstOrDefault(cls => cls.Name.ToLower() == name.ToLower());
    }

    public void SaveClass(CtfClass cls)
    {
      if (cls.Id == -1)
        try
        {
          _db.Query("INSERT INTO ctfclasses (Name, Description, HP, " +
                   "Mana, Inventory, Price, Hidden, Sell) " +
                   "VALUES (@0, @1, @2, @3, @4, @5, @6, @7)",
            cls.Name, cls.Description, cls.Hp, cls.Mana, string.Join("~", cls.Inventory),
            cls.Price, cls.Hidden ? 1 : 0, cls.Sell ? 1 : 0);
        }
        catch (Exception ex)
        {
          TShock.Log.Error(ex.ToString());
        }
      else
        try
        {
          _db.Query("UPDATE ctfclasses SET Name = @0, Description = @1, HP = @2, " +
                   "Mana = @3, Inventory = @4, Price = @5, Hidden = @6, Sell = @7 WHERE ID = @8",
            cls.Name, cls.Description, cls.Hp, cls.Mana, string.Join("~", cls.Inventory),
            cls.Price, cls.Hidden ? 1 : 0, cls.Sell ? 1 : 0, cls.Id);
        }
        catch (Exception ex)
        {
          TShock.Log.Error(ex.ToString());
        }
    }

    public void DeleteClass(int id)
    {
      try
      {
        _db.Query("DELETE FROM ctfclasses WHERE ID = @0", id);
      }
      catch (Exception ex)
      {
        TShock.Log.Error(ex.ToString());
      }
    }
  }
}