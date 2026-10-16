using GroupedScheduling.Hosting;
using GroupedScheduling.Sample;
using GroupedScheduling.Sample.Smoke;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddGroupedScheduling();
builder.Services.AddSingleton<ModesDemoWorker>();
builder.Services.AddSingleton<AuxWorker>();
builder.Services.AddSingleton<ValueTaskDemoWorker>();
builder.Services.AddSingleton<EntityStore<int>>();
builder.Services.AddSingleton<EntityStore<string>>();
builder.Services.AddSingleton<GenericMethodHost>();
builder.Services.AddSingleton(new MapperDemo<int>());

// Two distinct instances for cross-instance nest (A→B).
var lifeA = new LifecycleDemoWorker();
var lifeB = new LifecycleDemoWorker();
builder.Services.AddSingleton(lifeA);

using var host = builder.Build();
await host.StartAsync();

var worker = host.Services.GetRequiredService<ModesDemoWorker>();
var aux = host.Services.GetRequiredService<AuxWorker>();
var valueTasks = host.Services.GetRequiredService<ValueTaskDemoWorker>();
var intStore = host.Services.GetRequiredService<EntityStore<int>>();
var stringStore = host.Services.GetRequiredService<EntityStore<string>>();
var genericMethods = host.Services.GetRequiredService<GenericMethodHost>();
var mapper = host.Services.GetRequiredService<MapperDemo<int>>();
_ = lifeB;

await ArgAndNestedSmoke.RunAsync(worker, aux);
await ValueTaskSmoke.RunAsync(valueTasks);
await GenericsSmoke.RunAsync(intStore, stringStore, genericMethods);
await CombinedGenericsSmoke.RunAsync(mapper);
await StaticOnlySmoke.RunAsync();
await NestRejectSmoke.RunAsync();
await CrossInstanceNestSmoke.RunAsync(lifeA, lifeB);
await CancelInFlightSmoke.RunAsync(lifeA);
await ExceptionPropagateSmoke.RunAsync(lifeA);
await ParallelOverlapSmoke.RunAsync(worker);
await ModeRoundsSmoke.RunAsync(worker);
await HostStopSmoke.RunAsync(host, worker, aux, valueTasks, intStore, genericMethods, lifeA, mapper);
