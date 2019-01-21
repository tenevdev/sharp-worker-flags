# SpatialOS C# helper for using worker flags

[What are worker flags](https://docs.improbable.io/reference/latest/shared/worker-configuration/worker-flags#worker-flags)

## How to

This is a simple utility class that you should be able to drop in any project
which uses the SpatialOS C# SDK. Here's why you should use it:

  - Treats worker flags as an implementation detail
  - Separates value updates and parsing from how you use the data

I think this makes code cleaner, but YMMV so check out the example and compare
it to your own code:

```
// This file shows you what can be done with the worker flags updater
namespace Example
{
    // This could be any of your classes which could contain other properties, methods, etc.
    // The worker flags are just a detail, you shouldn't need to rearchitect your code to
    // make an existing property into a worker flag or remove one
    public class SpawnConfiguration
    {
        [WorkerFlag("spawn_z_offset", 0)]
        public int SpawnVerticalOffset { get; set; }

        [WorkerFlag("density_check_maximum", 20)]
        public int DensityCheckMax { get; set; }

        [WorkerFlag("density_check_distance", 300)]
        public int DensityCheckDistance { get; set; }
    }

    public class Program
    {
        private static void Main(string[] args)
        {
            // ... Get a dispatcher (view) somehow ...
            var dispatcher = new Dispatcher();

            // ... Get an instance of SpawnConfiguration somehow...
            var spawnConfig = new SpawnConfiguration();

            // Make an instance of the helper class which has a fluent interface
            var flags = new WorkerFlagsUpdater()
            
            // Register instances that you want to apply automatic updates to
            .RegisterAll(spawnConfig)

            // Or register your own parser for a specific flag by name
            .ParseWith("hidden_quests_csv", (option, metadata)
                => option.HasValue ? option.Value.Split(',') : metadata.DefaultValue)

            // Or register your own parser to apply to all properties of a given type
            // this example is the actual default enum parser so please just use it :)
            .ParseWith((option, metadata)
                => option.HasValue ? Enum.Parse(metadata.DefaultValue.GetType(), flag.Value) : metadata.DefaultValue);

            // Register the callback before you forget
            dispatcher.OnFlagUpdate(flags.OnFlagUpdate);

            // Process ops and enjoy the flag updates ...
        }
    }
}
```

## To do

  - Update multiple properties based on the same flag name
  - Generate worker flags JSON definitions from attributes
  - Add default and included parsers to cover more use cases
  - Compare performance to hand-crafted flag updates (no reflection)
  - Add a callback-based helper

Other contributions are also welcome!