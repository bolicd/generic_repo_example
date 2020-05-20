using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Infrastructure.Interfaces;

namespace Infrastructure.Repositories
{
    public abstract class GenericRepository<T> : IGenericRepository<T> where T: class
    {
        private readonly string _tableName;

        protected GenericRepository(string tableName)
        {
            _tableName = tableName;
        }
        /// <summary>
        /// Generate new connection based on connection string
        /// </summary>
        /// <returns></returns>
        private SqlConnection SqlConnection()
        {
            return new SqlConnection(ConfigurationManager.ConnectionStrings["MainDb"].ConnectionString);
        }

        /// <summary>
        /// Open new connection and return it for use
        /// </summary>
        /// <returns></returns>
        private IDbConnection CreateConnection()
        {
            var conn = SqlConnection();
            conn.Open();
            return conn;
        }

        private IEnumerable<PropertyInfo> GetProperties => typeof(T).GetProperties();


        public async Task<IEnumerable<T>> GetAllAsync()
        {
            using (var connection = CreateConnection())
            {
                //TODO: extract field names from T and use them to create query instead of using *
                return await connection.QueryAsync<T>($"SELECT * FROM {_tableName}");
            }
        }

        public async Task DeleteRowAsync(Guid id)
        {
            using (var connection = CreateConnection())
            {
                await connection.ExecuteAsync($"DELETE FROM {_tableName} WHERE Id=@Id", new { Id = id });
            }
        }

        public async Task<T> GetAsync(Guid id)
        {
            using (var connection = CreateConnection())
            {
                //TODO: extract field names from T and use them to create query instead of using *
                var result = await connection.QuerySingleOrDefaultAsync<T>($"SELECT * FROM {_tableName} WHERE Id=@Id", new { Id = id });
                if (result == null)
                    throw new KeyNotFoundException($"{_tableName} with id [{id}] could not be found.");

                return result;
            }
        }

        public async Task<int> SaveRangeAsync(IEnumerable<T> list)
        {
            var inserted = 0;
            var query = GenerateInsertQuery();
            using (var connection = CreateConnection())
            {
                inserted += await connection.ExecuteAsync(query, list);
            }

            return inserted;
        }

        public async Task UpdateAsync(T t)
        {
            var updateQuery = GenerateUpdateQuery();

            using (var connection = CreateConnection())
            {
                await connection.ExecuteAsync(updateQuery, t);
            }
        }

        public async Task InsertAsync(T t)
        {
            var insertQuery = GenerateInsertQuery();

            using (var connection = CreateConnection())
            {
                await connection.ExecuteAsync(insertQuery, t);
            }
        }

        #region helper private methods

        private string GenerateUpsertQuery()
        {
            //we assume
            var upsertQuery = new StringBuilder($"IF EXISTS (select * FROM {_tableName} WITH" +
                                                " (updlock, SERIALIZABLE) WHERE Id=@Id) " +
                                                $"BEGIN UPDATE {_tableName} SET ");

            
            var listOfProperties = typeof(T).GetProperties().Select(f => f.Name).ToList();
            var idPropertyType = typeof(T).GetProperties().Where(x => x.Name.Equals("Id")).Select(f => f.PropertyType).First();
            var insertId = idPropertyType.Name.Equals("Guid") || idPropertyType.Name.Equals("String");
            //add update part
            foreach (var prop in listOfProperties)
            {
                if (!insertId && prop.Equals("Id")) continue;

                upsertQuery.Append($"{prop}=@{prop},");
            }

            upsertQuery.Remove(upsertQuery.Length - 1, 1); //remove last comma
            upsertQuery.Append($" WHERE Id=@Id END ELSE BEGIN INSERT INTO {_tableName} ");
            upsertQuery.Append("(");
            foreach (var prop in listOfProperties)
            {
                if (!insertId && prop.Equals("Id")) continue;

                upsertQuery.Append($"[{prop}],");
            }
            upsertQuery
                .Remove(upsertQuery.Length - 1, 1)
                .Append(") VALUES (");

            foreach (var prop in listOfProperties)
            {
                if (!insertId && prop.Equals("Id")) continue;

                upsertQuery.Append($"@{prop},");
            }

            upsertQuery
                .Remove(upsertQuery.Length - 1, 1)
                .Append(") END");

            return upsertQuery.ToString();
        }

        private string GenerateUpdateQuery()
        {
            var updateQuery = new StringBuilder($"UPDATE {_tableName} SET ");
            var properties = GenerateListOfProperties(GetProperties);

            properties.ForEach(property =>
            {
                if (!property.Equals("Id"))
                {
                    updateQuery.Append($"{property}=@{property},");
                }
            });

            updateQuery.Remove(updateQuery.Length - 1, 1); //remove last comma
            updateQuery.Append(" WHERE Id=@Id");

            return updateQuery.ToString();
        }


        private static List<string> GenerateListOfProperties(IEnumerable<PropertyInfo> listOfProperties)
        {
            return (from prop in listOfProperties let attributes = prop.GetCustomAttributes(typeof(DescriptionAttribute), false)
                where attributes.Length <= 0 || (attributes[0] as DescriptionAttribute)?.Description != "ignore" select prop.Name).ToList();
        }

        private string GenerateInsertQuery()
        {
            var insertQuery = new StringBuilder($"INSERT INTO {_tableName} ");
            
            insertQuery.Append("(");

            var properties = GenerateListOfProperties(GetProperties);
            properties.ForEach(prop => { insertQuery.Append($"[{prop}],"); });

            insertQuery
                .Remove(insertQuery.Length - 1, 1)
                .Append(") VALUES (");

            properties.ForEach(prop => { insertQuery.Append($"@{prop},"); });

            insertQuery
                .Remove(insertQuery.Length - 1, 1)
                .Append(")");

            return insertQuery.ToString();
        }


        #endregion


    }
}
