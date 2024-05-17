using System.Xml;
using System.Xml.Serialization;
using UltraDES;

namespace UltraDES2PLC;

public class PLCprogram
{
    private readonly XmlDocument doc = new();
    private readonly List<object> ladder = new();
    private readonly HashSet<string> variables = new();

    public PLCprogram(DeterministicFiniteAutomaton[] sups, DeterministicFiniteAutomaton[] plants, Event[] events)
    {
        ulong element = 0;
        var disablement = events.Where(e => e.IsControllable).ToDictionary(e => e, e => new HashSet<string>());

        ladder.Add(LdLeftRail(element++));
        ladder.Add(LdLabel("supervisors", element++));

        for (var i = 0; i < sups.Length; i++)
        {
            var sup = sups[i];

            var trans = sup.Transitions.Where(t => t.Origin != t.Destination)
                .GroupBy(t => (t.Origin, t.Destination))
                .ToDictionary(g => g.Key, g => g.Select(t => t.Trigger).ToArray());

            foreach (var kvp in trans)
            {
                ladder.Add(LdComment(element++));
                ladder.Add(LdVendor(element++));

                var (o, d) = kvp.Key;
                var evs = kvp.Value;

                ladder.Add(LdContact(SupState(o, i), element++, [0]));

                var idState = element - 1;


                foreach (var ev in evs) ladder.Add(LdContact(ev.ToString(), element++, [idState]));

                var idContacts = Enumerable.Range((int)idState + 1, (int)(element - idState) - 1)
                    .Select(i => (ulong)i).ToArray();

                ladder.Add(LdCoil(SupState(d, i), storageModifierType.set, element++, idContacts));
                ladder.Add(LdCoil(SupState(o, i), storageModifierType.reset, element++, idContacts));
            }

            var controllableEvents = sup.Events.Where(e => e.IsControllable).ToArray();
            var transState = sup.Transitions.GroupBy(t => t.Origin)
                .ToDictionary(g => g.Key, g => g.Select(t => t.Trigger).Where(e => e.IsControllable).ToArray());
            var supDisablement =
                sup.States.ToDictionary(s => s, s => controllableEvents.Except(transState[s]).ToArray());

            foreach (var state in sup.States)
            foreach (var ev in supDisablement[state])
                disablement[(Event)ev].Add(SupState(state, i));
        }

        ladder.Add(LdLabel("disablement", element++));

        foreach (var kvp in disablement)
        {
            var ev = kvp.Key;
            var states = kvp.Value;

            ladder.Add(LdComment(element++));
            ladder.Add(LdVendor(element++));

            var idIni = element - 1;

            ladder.AddRange(states.Select(state => LdContact(state, element++, [0])));

            var idContacts = Enumerable.Range((int)idIni, (int)(element - idIni)).Select(i => (ulong)i).ToArray();

            ladder.Add(LdCoil("d_" + ev.Alias, storageModifierType.none, element++, idContacts));
        }

        ladder.Add(LdLabel("plants", element++));

        for (var i = 0; i < plants.Length; i++)
        {
            var plant = plants[i];

            var trans = plant.Transitions.OrderBy(t => t.Origin.ToString())
                .ThenBy(t => t.Trigger.IsControllable ? 1 : 0).Where(t => t.Origin != t.Destination).ToArray();

            foreach (var (o, ev, d) in trans)
            {
                ladder.Add(LdComment(element++));
                ladder.Add(LdVendor(element++));

                ladder.Add(LdContact(PlantState(o, i, plants), element++, [0]));

                var idState = element - 1;


                ladder.Add(ev.IsControllable
                    ? LdContact("d_" + ev, element++, [idState], true)
                    : LdContact("p_" + ev, element++, [idState]));

                var idContacts = new[] { element - 1 };

                if (ev.IsControllable)
                {
                    ladder.Add(LdCoil("p_" + ev, storageModifierType.none, element++, idContacts));
                    ladder.Add(LdCoil(ev.ToString(), storageModifierType.none, element++, idContacts));
                }
                else
                {
                    ladder.Add(LdCoil(ev.ToString(), storageModifierType.none, element++, idContacts));
                    //ladder.Add(LdCoil("p_" + ev, storageModifierType.none, element++, idContacts));
                }

                ladder.Add(LdCoil(PlantState(d, i, plants), storageModifierType.set, element++, idContacts));
                ladder.Add(LdCoil(PlantState(o, i, plants), storageModifierType.reset, element++, idContacts));
                ladder.Add(LdJump("supervisors", element++,
                    idContacts)); //originally supervisors, but changed to procedures
            }
        }

        ladder.Add(LdLabel("procedures", element++));

        ladder.Add(LdComment(element++));
        ladder.Add(LdVendor(element++));
        ladder.Add(LdContact("START", element++, [0]));
        var idStart = new[] { element - 1 };

        for (var i = 0; i < sups.Length; i++)
            ladder.Add(LdCoil(SupState(sups[i].InitialState, i), storageModifierType.set, element++, idStart));

        for (var i = 0; i < plants.Length; i++)
            ladder.Add(LdCoil(PlantState(plants[i].InitialState, i, plants), storageModifierType.set, element++, idStart));

        ladder.Add(LdRightRail());
    }

    public string ProjectName { get; set; } = "ultrades.project";
    public string PouName { get; set; } = "DES_PRG";

    private string SupState(AbstractState s, int k) => $"s{k:00}_" + s.ToString().Replace("|", "_");
    private string PlantState(AbstractState s, int k, DeterministicFiniteAutomaton[] p) => $"{p[k]}_" + s.ToString().Replace("|", "_");//$"p{k:00}_" + s.ToString().Replace("|", "_");

    public void ToXMLFile(string path)
    {
        CreateXMLProjet(variables.ToArray(), ladder.ToArray(), path);
    }

    private void CreateXMLProjet(string[] variables, object[] ladder, string nomeProjeto)
    {
        var pj = new project();
        pj.fileHeader = new projectFileHeader
        {
            companyName = "LACSED",
            productName = "UltraDES",
            productVersion = "1.0",
            creationDateTime = DateTime.Now
        };

        pj.contentHeader = new projectContentHeader
        {
            name = ProjectName,
            modificationDateTime = DateTime.Now
        };
        pj.contentHeader.coordinateInfo = new projectContentHeaderCoordinateInfo();
        pj.contentHeader.coordinateInfo.fbd = new projectContentHeaderCoordinateInfoFbd();
        pj.contentHeader.coordinateInfo.fbd.scaling = new projectContentHeaderCoordinateInfoFbdScaling
        {
            x = 1,
            y = 1
        };

        pj.contentHeader.coordinateInfo.ld = new projectContentHeaderCoordinateInfoLD();
        pj.contentHeader.coordinateInfo.ld.scaling = new projectContentHeaderCoordinateInfoLDScaling
        {
            x = 1,
            y = 1
        };

        pj.contentHeader.coordinateInfo.sfc = new projectContentHeaderCoordinateInfoSfc();
        pj.contentHeader.coordinateInfo.sfc.scaling = new projectContentHeaderCoordinateInfoSfcScaling
        {
            x = 1,
            y = 1
        };

        pj.contentHeader.addData =
        [
            new addDataData
            {
                name = "http://www.3s-software.com/plcopenxml/projectinformation",
                handleUnknown = addDataDataHandleUnknown.implementation,
                Any = doc.CreateElement("ProjectInformation")
            }
        ];

        pj.types = new projectTypes
        {
            dataTypes = []
        };

        var pou = new projectTypesPou
        {
            name = PouName,
            pouType = pouType.program,
            @interface = new projectTypesPouInterface()
        };
        pou.@interface.Items = new[] { new projectTypesPouInterfaceLocalVars() };
        pou.@interface.Items[0].variable = variables.Select(n => new varListPlainVariable
        {
            name = n,
            type = new dataType { ItemElementName = ItemChoiceType1.BOOL, Item = false }
        }).ToArray();

        pou.body = [new body()];
        var ld = new bodyLD();

        pou.body[0].Item = ld;
        pou.body[0].ItemElementName = ItemChoiceType.LD;

        ld.Items = ladder;

        pj.types.pous = [pou];
        pj.instances = new projectInstances([]);


        var projectStructure = doc.CreateElement("ProjectStructure");
        var objectElement = doc.CreateElement("Object");
        objectElement.SetAttribute("Name", "PLC_PRG");
        objectElement.SetAttribute("ObjectId", "41dd9df7-66e1-44a1-8afe-609cdf6d59c1");
        projectStructure.AppendChild(objectElement);

        pj.addData =
        [
            new addDataData
            {
                name = "http://www.3s-software.com/plcopenxml/projectstructure",
                handleUnknown = addDataDataHandleUnknown.discard,
                Any = projectStructure
            }
        ];

        var serializer = new XmlSerializer(typeof(project));

        using StreamWriter fileWriter = new(nomeProjeto);
        serializer.Serialize(fileWriter, pj);
    }

    private bodyFBDComment LdComment(ulong id) =>
        new()
        {
            localId = id,
            position = new position { x = 0, y = 0 },
            content = doc.CreateElement("xhtml", "http://www.w3.org/1999/xhtml")
        };

    private bodyLDLeftPowerRail LdLeftRail(ulong id = 0) =>
        new()
        {
            localId = id,
            position = new position { x = 0, y = 0 },
            connectionPointOut = new[] { new bodyLDLeftPowerRailConnectionPointOut { formalParameter = "none" } }
        };

    private bodyFBDVendorElement LdVendor(ulong id)
    {
        var elementType = doc.CreateElement("ElementType");
        elementType.InnerText = "networktitle";
        return new bodyFBDVendorElement
        {
            localId = id,
            position = new position { x = 0, y = 0 },
            alternativeText = doc.CreateElement("xhtml", "http://www.w3.org/1999/xhtml"),
            addData =
            [
                new addDataData
                {
                    name = "http://www.3s-software.com/plcopenxml/fbdelementtype",
                    handleUnknown = addDataDataHandleUnknown.implementation,
                    Any = elementType
                }
            ]
        };
    }

    private bodyLDContact LdContact(string name, ulong id, ulong[] connectTo, bool negated = false)
    {
        variables.Add(name);
        return new bodyLDContact
        {
            localId = id,
            negated = negated,
            storage = storageModifierType.none,
            edge = edgeModifierType.none,
            position = new position { x = 0, y = 0 },
            connectionPointIn = new connectionPointIn
            {
                Items = connectTo.Select(i => (object) new connection { refLocalId = i }).ToArray()
            },
            connectionPointOut = new connectionPointOut(),
            variable = name
        };
    }

    private bodyLDCoil LdCoil(string name, storageModifierType type, ulong id, ulong[] connectTo)
    {
        variables.Add(name);
        return new bodyLDCoil
        {
            localId = id,
            negated = false,
            storage = type,
            position = new position { x = 0, y = 0 },
            connectionPointIn = new connectionPointIn
            {
                Items = connectTo.Select(i => (object)new connection { refLocalId = i }).ToArray()
            },
            connectionPointOut = new connectionPointOut(),
            variable = name
        };
    }

    private bodyLDRightPowerRail LdRightRail() =>
        new()
        {
            localId = 2147483646,
            position = new position { x = 0, y = 0 },
            connectionPointIn = new[] { new connectionPointIn() }
        };

    private bodyFBDLabel LdLabel(string label, ulong id) =>
        new()
        {
            localId = id,
            position = new position { x = 0, y = 0 },
            label = label
        };

    private bodyFBDJump LdJump(string label, ulong id, ulong[] connectTo) =>
        new()
        {
            localId = id,
            position = new position { x = 0, y = 0 },
            label = label,
            connectionPointIn = new connectionPointIn
            {
                Items = connectTo.Select(i => (object) new connection { refLocalId = i }).ToArray()
            }
        };
}