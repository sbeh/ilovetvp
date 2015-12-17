﻿using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ilovetvp
{
    abstract class Event
    {
        public int id;
        public Character character;
        public DateTime timestamp;

        public abstract void persist();

        public static Event create(Character character, string message)
        {
            Event ret;

            var match_shot = regexp_shot.Match(message);
            if (match_shot.Success)
                ret = new CombatEvent()
                {
                    character = character,
                    timestamp = DateTime.ParseExact(match_shot.Groups[@"timestamp"].Value, @"yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture),
                    damage = int.Parse(match_shot.Groups[@"damage"].Value),
                    enemy = match_shot.Groups[@"enemy"].Value,
                    weapon = match_shot.Groups[@"weapon"].Value,
                };
            else
                ret = new UnknownEvent()
                {
                    character = character,
                    message = message,
                };

            ret.persist();
            character.addEvent(ret);

            return ret;
        }

        private static Regex regexp_shot = new Regex(@"\[ (?<timestamp>\d\d\d\d\.\d\d\.\d\d \d\d:\d\d:\d\d) \] \(combat\) <color=0xff00ffff><b>(?<damage>\d+)</b> <color=0x77ffffff><font size=10>to</font> <b><color=0xffffffff>(?<enemy>[^<]+)</b><font size=10><color=0x77ffffff> - (?<weapon>.+(?= - ))", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
    }

    class CombatEvent : Event
    {
        static SqlCommand insert = new SqlCommand(@"INSERT INTO Event (Character, Timestamp, Type) OUTPUT INSERTED.ID VALUES (@character, @timestamp, 1)", analyzer.db);
        static SqlCommand insertCombat = new SqlCommand(@"INSERT INTO Event_Combat (ID, Damage, Enemy, Weapon) VALUES (@id, @damage, @enemy, @weapon)", analyzer.db);
        static CombatEvent()
        {
            insert.Parameters.Add(@"@character", SqlDbType.Int);
            insert.Parameters.Add(@"@timestamp", SqlDbType.DateTime);
            insertCombat.Parameters.Add(@"@id", SqlDbType.Int);
            insertCombat.Parameters.Add(@"@damage", SqlDbType.Int);
            insertCombat.Parameters.Add(@"@enemy", SqlDbType.NVarChar, 80);
            insertCombat.Parameters.Add(@"@weapon", SqlDbType.NVarChar, 40);
        }

        public int damage;
        public string enemy;
        public string weapon;

        public override void persist()
        {
            lock(analyzer.db)
            {
                insert.Parameters[@"@character"].Value = character.id;
                insert.Parameters[@"@timestamp"].Value = timestamp;
                insert.Prepare();
                id = (int)insert.ExecuteScalar();
            }

            lock (analyzer.db)
            {
                insertCombat.Parameters[@"@id"].Value = id;
                insertCombat.Parameters[@"@damage"].Value = damage;
                insertCombat.Parameters[@"@enemy"].Value = enemy;
                insertCombat.Parameters[@"@weapon"].Value = weapon;
                insertCombat.Prepare();
                if (insertCombat.ExecuteNonQuery() != 1)
                    throw new Exception(@"Failed to execute statement: " + insertCombat.CommandText);
            }
        }

        public override string ToString()
        {
            return string.Format(@"[ {0:yyyy.MM.dd HH:mm:ss} ] (combat) {1} to {2} - {3}", timestamp, damage, enemy, weapon);
        }
    }

    class UnknownEvent : Event
    {
        static SqlCommand insert = new SqlCommand(@"INSERT INTO Event (Character, Type, Message) OUTPUT INSERTED.ID VALUES (@character, 0, @message)", analyzer.db);
        static UnknownEvent()
        {
            insert.Parameters.Add(@"@character", SqlDbType.Int);
            insert.Parameters.Add(@"@message", SqlDbType.NText, (int)Math.Pow(2, 30) - 1);
        }

        public string message;

        public override void persist()
        {
            lock (analyzer.db)
            {
                insert.Parameters[@"@character"].Value = character.id;
                insert.Parameters[@"@message"].Value = message;
                insert.Prepare();
                id = (int)insert.ExecuteScalar();
            }
        }

        public override string ToString()
        {
            return message;
        }
    }
}
