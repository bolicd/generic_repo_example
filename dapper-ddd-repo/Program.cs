using System;
using System.Threading.Tasks;
using Infrastructure.Models;
using Infrastructure.Repositories;

namespace dapper_ddd_repo
{
    class Program
    {
        // Use example for GenericRepository       
        public static async Task Main(string[] args)
        {
            // create new Repository for table Users
            var userRepository = new UserRepository("Users");
            Console.WriteLine(" Save into table users ");
            var guid = Guid.NewGuid();
            await userRepository.InsertAsync(new User()
            {
                FirstName = "Test2",
                Id = guid,
                LastName = "LastName2"
            });


            await userRepository.UpdateAsync(new User()
            {
                FirstName = "Test3",
                Id = guid,
                LastName = "LastName3"
            });


            var user = await userRepository.GetAsync(guid);
            Console.WriteLine($"Fetched User {user.FirstName}");
            Console.ReadLine();
        }
    }
}
