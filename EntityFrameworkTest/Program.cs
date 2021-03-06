﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using AutoMapper;
using AutoMapper.EntityFrameworkCore;
using AutoMapper.EquivalencyExpression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;

namespace EntityFrameworkTest
{
    public class Parent
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public List<Child> Children { get; set; }
    }

    public class Child
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        
        public string Value { get; set; }
    }

    public sealed class EntityDbContext : DbContext
    {
        public DbSet<Parent> Parents { get; set; }
        
        private readonly Action<DbContextOptionsBuilder> _onConfiguring;

        /// <inheritdoc />
        /// <summary>
        /// Constructor that will be called by startup.cs
        /// </summary>
        /// <param name="dbContextOptionsBuilderAction"></param>
        public EntityDbContext(Action<DbContextOptionsBuilder> dbContextOptionsBuilderAction)
        {
            _onConfiguring = dbContextOptionsBuilderAction;

            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => _onConfiguring(optionsBuilder);
    }

    internal class ParentProfile : Profile
    {
        public ParentProfile()
        {
            CreateMap<Parent, Parent>()
                .ForMember(x => x.Children, opt => opt.UseDestinationValue())
                .ReverseMap();
        }
    }

    internal class ChildProfile : Profile
    {
        public ChildProfile()
        {
            CreateMap<Child, Child>()
                .ReverseMap();
        }
    }
    
    internal class Program
    {
        private static void Main(string[] args)
        {
            // Intialize the database context
            var context = new EntityDbContext(x => x.UseInMemoryDatabase("Database"));

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddSingleton(context);
            
            // Initialize the AutoMapper
            var mapper = new MapperConfiguration(x =>
            {
                x.AddProfile<ChildProfile>();
                x.AddProfile<ParentProfile>();
                x.AddCollectionMappers();
                x.UseEntityFrameworkCoreModel<EntityDbContext>(serviceCollection);
            }).CreateMapper();
            
            // Create original instance
            var instance = new Parent
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        Value = "X1"
                    }
                }
            };
            
            // Add instance to DbContext
            context.Parents.Persist(mapper).InsertOrUpdate(instance);

            // Save changes
            context.SaveChanges();

            // Try to get the instance back
            var entity = context.Parents.First();

            // Serialize and de-serialize the instance to simulate an object being sent from API layer
            var updatedInstance = JsonConvert.DeserializeObject<Parent>(JsonConvert.SerializeObject(entity));

            // Create a new child object
            var newChild = new Child
            {
                Value = "X2"
            };
            
            // Add the child to the API generated object
            updatedInstance.Children.Add(newChild);

            // Apply changes from updatedInstance back to entity
            // mapper.Map(updatedInstance, entity);
            
            context.Parents.Persist(mapper).InsertOrUpdate(updatedInstance);

            // Save changed
            context.SaveChanges();
            
            entity = context.Parents.First();
            
            Console.ReadKey();
        }
    }
}