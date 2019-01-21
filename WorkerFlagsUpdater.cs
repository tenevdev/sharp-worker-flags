using System;
using System.Collections.Generic;
using System.Reflection;
using Improbable.Collections;
using Improbable.Worker;

namespace WorkerFlags
{
    public class WorkerFlagsUpdater
    {
        private readonly IDictionary<string, WorkerFlagAttribute> flagNameToMetadata = new Dictionary<string, WorkerFlagAttribute>();
        private readonly IDictionary<string, Tuple<object, PropertyInfo>> flagNameToProp = new Dictionary<string, Tuple<object, PropertyInfo>>();
        private readonly IDictionary<string, IWorkerFlagParser> flagNameToParser = new Dictionary<string, IWorkerFlagParser>();

        private static readonly IDictionary<Type, IWorkerFlagParser> DefaultParsers = new Dictionary<Type, IWorkerFlagParser>()
        {
            { typeof(int), new IntFlagParser() },
            { typeof(Enum), new EnumFlagParser() },
        };

        public WorkerFlagsUpdater()
        {
        }

        /// <summary>
        /// Configure all properties of <paramref name="container"/> with the WorkerFlag attribute to be updated when OnFlagUpdate is called.
        /// </summary>
        /// <typeparam name="T">Type of container instance</typeparam>
        /// <param name="container">An instance which has WorkerFlag attributes on some properties</param>
        /// <returns></returns>
        public WorkerFlagsUpdater RegisterAll<T>(T container)
        {
            foreach (var propertyInfo in typeof(T).GetProperties())
            {
                foreach (WorkerFlagAttribute workerFlag in propertyInfo.GetCustomAttributes(typeof(WorkerFlagAttribute), false))
                {
                    flagNameToProp[workerFlag.Name] = Tuple.Create((object)container, propertyInfo);
                    flagNameToMetadata[workerFlag.Name] = workerFlag;

                    if (flagNameToParser.ContainsKey(workerFlag.Name))
                    {
                        // a parser already exists for this flag (perhaps a custom parser)
                        continue;
                    }

                    if (propertyInfo.PropertyType.IsEnum)
                    {
                        flagNameToParser[workerFlag.Name] = DefaultParsers[typeof(Enum)];
                    }

                    if (!DefaultParsers.ContainsKey(propertyInfo.PropertyType))
                    {
                        throw new ArgumentException($"There is no default type parser for {propertyInfo.Name} of type {propertyInfo.PropertyType} used for flag {workerFlag.Name}. You can fix this by specifying a custom type parser.");
                    }

                    flagNameToParser[workerFlag.Name] = DefaultParsers[propertyInfo.PropertyType];
                }
            }

            return this;
        }

        /// <summary>
        /// Register a parser to use for a specific worker flag name.
        /// </summary>
        /// <typeparam name="T">Result value type after parsing the flag</typeparam>
        /// <param name="name">Name of the worker flag to apply this custom parser for</param>
        /// <param name="parser">Parser function</param>
        /// <returns>Fluent</returns>
        public WorkerFlagsUpdater ParseWith<T>(string name, Func<Option<string>, WorkerFlagAttribute, T> parser)
        {
            flagNameToParser[name] = new CustomFlagParser<T>(parser);
            return this;
        }

        /// <summary>
        /// Register a parser to use for all worker flag properties of a given type from now on.
        /// Properties of the same type which have already been registered don't have their parser updated.
        /// </summary>
        /// <typeparam name="T">The type to use this parser for</typeparam>
        /// <param name="parser">Parser function</param>
        /// <returns>Fluent</returns>
        public WorkerFlagsUpdater ParseWith<T>(Func<Option<string>, WorkerFlagAttribute, T> parser)
        {
            DefaultParsers[typeof(T)] = new CustomFlagParser<T>(parser);
            return this;
        }

        /// <summary>
        /// Method to use for OnFlagUpdate dispatcher callback.
        /// </summary>
        /// <param name="update">Dispatcher callback flag update</param>
        public void OnFlagUpdate(FlagUpdateOp update)
        {
            var flagProp = flagNameToProp[update.Name];
            var obj = flagProp.Item1;
            var propertyInfo = flagProp.Item2;
            propertyInfo.SetValue(obj, flagNameToParser[update.Name].Parse(update.Value, flagNameToMetadata[update.Name]));
        }
    }

    public class WorkerFlagAttribute : Attribute
    {
        public WorkerFlagAttribute(string name, object defaultValue = null)
        {
            Name = name;
            DefaultValue = defaultValue;
        }

        public string Name { get; set; }
        public object DefaultValue { get; set; }
    }

    public interface IWorkerFlagParser
    {
        object Parse(Option<string> value, WorkerFlagAttribute metadata);
    }

    public class IntFlagParser : IWorkerFlagParser
    {
        public object Parse(Option<string> flag, WorkerFlagAttribute metadata)
        {
            if (!flag.HasValue)
            {
                return metadata.DefaultValue;
            }

            if (int.TryParse(flag.Value, out var res))
            {
                return res;
            }

            throw new ArgumentException(
                $"Worker flag {metadata.Name} set to a value {flag.Value} which could not be parsed to an int.");
        }
    }

    public class CustomFlagParser<T> : IWorkerFlagParser
    {
        private readonly Func<Option<string>, WorkerFlagAttribute, T> parser;

        public CustomFlagParser(Func<Option<string>, WorkerFlagAttribute, T> parser)
        {
            this.parser = parser;
        }

        public object Parse(Option<string> value, WorkerFlagAttribute metadata)
        {
            return parser(value, metadata);
        }
    }

    public class EnumFlagParser : IWorkerFlagParser
    {
        public object Parse(Option<string> flag, WorkerFlagAttribute metadata)
        {
            if (!flag.HasValue)
            {
                return metadata.DefaultValue;
            }

            return Enum.Parse(metadata.DefaultValue.GetType(), flag.Value);
        }
    }
}
