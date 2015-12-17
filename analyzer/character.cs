using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;

namespace ilovetvp
{
    class Character
    {
#if !NO_DB
        static SqlCommand search = new SqlCommand(@"SELECT ID FROM Character WHERE Name = @name", analyzer.db);
        static SqlCommand insert = new SqlCommand(@"INSERT INTO Character (Name) OUTPUT INSERTED.ID VALUES (@name)", analyzer.db);
        static Character()
        {
            search.Parameters.Add(@"@name", SqlDbType.NVarChar, 80);
            insert.Parameters.Add(@"@name", SqlDbType.NVarChar, 80);
        }

        public int id;
#endif
        public string name;
        private IPEndPoint _endpoint;
        public IPEndPoint endpoint
        {
            set
            {
                if(!object.Equals(_endpoint, value))
                    Util.debug(@"{0} connected from {1}:{2}", name, value.Address, value.Port);

                _endpoint = value;
            }
            get
            {
                return _endpoint;
            }
        }
        public int damage;

        private Character(string name)
        {
            this.name = name;

#if !NO_DB
            lock(analyzer.db)
            {
                // start searching database for character name
                search.Parameters[@"@name"].Value = name;
                search.Prepare();
                var res = search.ExecuteScalar();
                if (res == null) // character not in database
                {
                    insert.Parameters[@"@name"].Value = name;
                    insert.Prepare();
                    res = insert.ExecuteScalar();
                }
                id = (int)res;
            }
#endif
        }

        #region Event
        private List<Event> events = new List<Event>();

        public event Action<Event> EventAdded;

        public void addEvent(Event e)
        {
            //events.Add(e);

            try
            {
                damage += ((CombatEvent)e).damage;
            }
            catch { }

            var listener = EventAdded;
            if (listener != null)
                listener(e);
        }
        #endregion

        #region Singleton
        static Dictionary<string, Character> characters = new Dictionary<string, Character>();

        public static Character get(string name)
        {
            try
            {
                return characters[name];
            }
            catch
            {
                // character not yet loaded into memory
            }

            Util.debug(@"{0} | New Character", name);

            // create singleton
            var character = new Character(name);
            characters.Add(character.name, character);
            return character;
        }
        #endregion
    }
}
