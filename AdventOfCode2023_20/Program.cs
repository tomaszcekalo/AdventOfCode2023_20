// See https://aka.ms/new-console-template for more information
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;

Console.WriteLine("Hello, World!");

var input = @"%qm -> mj, xn
&mj -> hz, bt, lr, sq, qh, vq
%qc -> qs, vg
%ng -> vr
%qh -> sq
&bt -> rs
%hh -> qs, bx
%gk -> cs, bb
%js -> mj
%pc -> mj, mr
%mb -> rd, xs
%tp -> qs, ks
%xq -> tp, qs
%bx -> sz
%mn -> cs, md
%cv -> rd
%rh -> rd, sv
%md -> cs
%pz -> mj, vq
%bz -> rd, hk
%jz -> vk
%sz -> jz
%lr -> pz, mj
%xs -> cv, rd
%kl -> rd, mb
%hz -> pc
%hk -> rz, rd
%vk -> qc
%bh -> zm
%vq -> qm
%ks -> qs, nd
&qs -> dl, jz, bx, vk, vg, hh, sz
&dl -> rs
%lf -> rh, rd
&fr -> rs
%xn -> mj, qh
%hf -> qs, xq
%sv -> rd, ng
&rs -> rx
&rd -> ng, fr, rz, lf, vr
%cj -> ss, cs
broadcaster -> hh, lr, bp, lf
%zs -> cs, mn
%vr -> bz
%nd -> qs
%jb -> cj, cs
&rv -> rs
%bp -> cs, lx
%ss -> zs
%lx -> gk
&cs -> lx, ss, rv, bh, bp
%bb -> bh, cs
%mf -> mj, hz
%zm -> cs, jb
%mr -> mj, js
%rz -> kl
%vg -> hf
%sq -> mf";

var inputSmall =
    @"broadcaster -> a, b, c
%a -> b
%b -> c
%c -> inv
&inv -> a";

var inputSmall2 = @"broadcaster -> a
%a -> inv, con
&inv -> b
%b -> con
&con -> output";

var reader = new ModuleConfigurationReader();
//var moduleConfiguration = reader.ReadModuleConfiguration(inputSmall);
//moduleConfiguration.SendLowPulse();
//var moduleConfiguration = reader.ReadModuleConfiguration(inputSmall2);
//for (int i = 1; i <= 4; i++)
//{
//    Console.WriteLine($"---Run {i}:");
//    moduleConfiguration.SendLowPulse();
//}
var moduleConfiguration = reader.ReadModuleConfiguration(input);
Dictionary<bool, long> pulseCounter = new Dictionary<bool, long>();
pulseCounter[false] = 0;
pulseCounter[true] = 0;
var runs = 1000;
for (int i = 1; i <= runs; i++)
{
    var result = moduleConfiguration.SendLowPulse();
    pulseCounter[false] += result.Counter[false];
    pulseCounter[true] += result.Counter[true];
    if (result.LowPulseToRxReceived)
        break;
}
Console.WriteLine(
    $@"after pushing the button {runs} times, {pulseCounter[false]} low pulses and {pulseCounter[true]} high pulses are sent.
Multiplying these together gives {pulseCounter[false] * pulseCounter[true]}.");

public class SendPulseResult
{
    public Dictionary<bool, int> Counter { get; set; }
    public bool LowPulseToRxReceived { get; set; }
}

public class ModuleConfiguration
{
    public ModuleConfiguration(Dictionary<string, IModule> modules)
    {
        Modules = modules;
    }

    public Dictionary<string, IModule> Modules { get; }

    public SendPulseResult SendLowPulse(bool showLog = false)
    {
        Queue<(string from, string to, bool signal)> pulses = new Queue<(string, string, bool)>();
        pulses.Enqueue(("button", "broadcaster", false));
        Dictionary<bool, int> pulseCounter = new Dictionary<bool, int>();
        pulseCounter[false] = 0;
        pulseCounter[true] = 0;
        var rxLowReceived = false;

        while (pulses.Count > 0)
        {
            var pulse = pulses.Dequeue();
            pulseCounter[pulse.signal] += 1;

            if (showLog)
                Console.WriteLine($"{pulse.from} -{(pulse.signal ? "high" : "low")}-> {pulse.to}");

            if (pulse.to == "rx" && !pulse.signal)
            {
                rxLowReceived = true;
            }
            if (Modules.ContainsKey(pulse.to))
            {
                var toAdd = Modules[pulse.to].GetSignals(pulse.from, pulse.signal);
                foreach (var item in toAdd)
                {
                    pulses.Enqueue((pulse.to, item.Destination, item.Signal));
                }
            }
        }
        if (showLog)
            Console.WriteLine($"{pulseCounter[false]} low pulses and {pulseCounter[true]} high pulses are sent.");
        return new SendPulseResult()
        {
            LowPulseToRxReceived = rxLowReceived,
            Counter = pulseCounter
        };
    }
}

public class ModuleConfigurationReader
{
    private Dictionary<char, IModuleCreator> _moduleCreators;

    public ModuleConfigurationReader()
    {
        _moduleCreators = new Dictionary<char, IModuleCreator>
        {
            { 'b', new BroadcastModuleCreator() },
            { '%', new FlipFlopModuleCreator() },
            { '&', new ConjunctionModuleCreator() }
        };
    }

    public ModuleConfiguration ReadModuleConfiguration(string input)
    {
        var lines = input.Split(Environment.NewLine);
        Dictionary<string, IModule> modules = new Dictionary<string, IModule>();
        foreach (var line in lines)
        {
            var setup = line.Split("->");
            var module = _moduleCreators[line[0]].CreateModule(setup[0], setup[1]);
            modules.Add(module.Id, module);
        }
        foreach (var module in modules.Values.Where(x => x is ConjunctionModule))
        {
            var cm = module as ConjunctionModule;
            cm.Inputs = modules.Values
                .Where(x => x.Destinations.Contains(module.Id))
                .ToDictionary(x => x.Id, x => false);
        }
        return new ModuleConfiguration(modules);
    }
}

public interface IModule
{
    public string Id { get; set; }
    public IEnumerable<string> Destinations { get; set; }

    public IEnumerable<Pulse> GetSignals(string from, bool impulse);
}

public class FlipFlopModule : IModule
{
    public string Id { get; set; }
    public IEnumerable<string> Destinations { get; set; }
    private bool State { get; set; }

    public IEnumerable<Pulse> GetSignals(string from, bool impulse)
    {
        if (impulse)
            return new List<Pulse>();
        State = !State;

        return Destinations.Select(x => new Pulse()
        {
            Destination = x,
            Signal = State
        });
    }
}

public class Pulse
{
    public string Destination { get; set; }
    public bool Signal { get; set; }
}

public class ConjunctionModule : IModule
{
    public string Id { get; set; }
    public IEnumerable<string> Destinations { get; set; }
    public Dictionary<string, bool> Inputs { get; set; }

    public ConjunctionModule()
    {
        //Inputs = new Dictionary<string, bool>();
    }

    public IEnumerable<Pulse> GetSignals(string from, bool impulse)
    {
        Inputs[from] = impulse;
        var state = !Inputs.All(x => x.Value);

        return Destinations.Select(x => new Pulse()
        {
            Destination = x,
            Signal = state
        });
    }
}

public class BroadcastModule : IModule
{
    public string Id { get; set; }
    public IEnumerable<string> Destinations { get; set; }

    public IEnumerable<Pulse> GetSignals(string from, bool impulse)
    {
        return Destinations.Select(x => new Pulse()
        {
            Destination = x,
            Signal = impulse
        });
    }
}

public interface IModuleCreator
{
    public IModule CreateModule(string name, string destination);
}

public class FlipFlopModuleCreator : IModuleCreator
{
    public IModule CreateModule(string name, string destination)
    {
        return new FlipFlopModule()
        {
            Id = name.Substring(1).Trim(),
            Destinations = destination.Split(',')
                .Select(x => x.Trim())
        };
    }
}

public class ConjunctionModuleCreator : IModuleCreator
{
    public IModule CreateModule(string name, string destination)
    {
        return new ConjunctionModule()
        {
            Id = name.Substring(1).Trim(),
            Destinations = destination.Split(',')
                .Select(x => x.Trim())
        };
    }
}

public class BroadcastModuleCreator : IModuleCreator
{
    public IModule CreateModule(string name, string destination)
    {
        return new BroadcastModule()
        {
            Id = name.Trim(),
            Destinations = destination.Split(',')
                .Select(x => x.Trim())
        };
    }
}