﻿using UltraDES;
using UltraDES2PLC;

var s1 = new State("s1", Marking.Marked);
var s2 = new State("s2", Marking.Unmarked);
var s3 = new State("s3", Marking.Unmarked);
var s4 = new State("s4", Marking.Unmarked);
var s5 = new State("s5", Marking.Unmarked);
var s6 = new State("s6", Marking.Unmarked);
var s7 = new State("s7", Marking.Unmarked);


var ep = new Event("ep", Controllability.Controllable);           // main conveyor turns on
var cg = new Event("cg", Controllability.Uncontrollable);         // big box
var cp = new Event("cp", Controllability.Uncontrollable);         // small box
var sp_r = new Event("sp_r", Controllability.Uncontrollable);     // pallet sensor rising
var sp_f = new Event("sp_f", Controllability.Uncontrollable);     // pallet sensor falling
var see_r = new Event("see_r", Controllability.Uncontrollable);   // left entrance sensor rising
var see_f = new Event("see_f", Controllability.Uncontrollable);   // left entrance sensor falling
var sse_r = new Event("sse_f", Controllability.Uncontrollable);   // left exit sensor rising
var sed_r = new Event("sed_r", Controllability.Uncontrollable);   // right entrance sensor rising
var sed_f = new Event("sed_f", Controllability.Uncontrollable);   // right entrance sensor falling
var ssd_r = new Event("ssd_f", Controllability.Uncontrollable);   // right exit sensor rising
var ee = new Event("ee", Controllability.Controllable);           // left conveyor turns on
var ed = new Event("ed", Controllability.Controllable);           // right conveyor turns on
var cf = new Event("cf", Controllability.Controllable);           // front center
var ce = new Event("ce", Controllability.Controllable);           // left center
var cd = new Event("cd", Controllability.Controllable);           // right center
var spos = new Event("spos", Controllability.Uncontrollable); // position sensor rising

var EP = new DeterministicFiniteAutomaton(new Transition[]
{
    (s1, ep, s2),
    (s2, sp_r, s3),
    (s3, cg, s4),
    (s3, cp, s4),
    (s4, sp_f, s1),
}, s1, "EP"); // Main Conveyor

var EE = new DeterministicFiniteAutomaton(new Transition[]
{
    (s1, see_f, s2),
    (s2, see_r, s3),
    (s3, ee, s4),
    (s4, sse_r, s1)
}, s1, "EE"); // Left Conveyor

var ED = new DeterministicFiniteAutomaton(new Transition[]
{
    (s1, sed_f, s2),
    (s2, sed_r, s3),
    (s3, ed, s4),
    (s4, ssd_r, s1)
}, s1, "ED"); // Right Conveyor

var EC = new DeterministicFiniteAutomaton(new Transition[]
{
    (s1, sp_r, s2),
    (s2, sp_f, s3),
    (s3, cf, s4),
    (s4, spos, s5),
    (s5, cd, s6),
    (s6, sed_r, s1),
    (s5, ce, s7),
    (s7, see_r, s1)
}, s1, "EC"); // Central Conveyor

var E1 = new DeterministicFiniteAutomaton(new Transition[]
{
    (s1, sp_f, s2),
    (s2, cf, s1),
}, s1, "E1"); // Specification 1

var E2 = new DeterministicFiniteAutomaton(new Transition[]
{
    (s1, cg, s2),
    (s2, cd, s1),
    (s1, cp, s3),
    (s3, ce, s1),
}, s1, "E2"); // Specification 2

var E3 = new DeterministicFiniteAutomaton(new Transition[]
{
    (s1, see_r, s2),
    (s2, ee, s1),
}, s1, "E3"); // Specification 3

var E4 = new DeterministicFiniteAutomaton(new Transition[]
{
    (s1, sed_r, s2),
    (s2, ed, s1),
}, s1, "E4"); // Specification 4


var plants = new[] { EC, ED, EP, EE };
var specs = new[] { E2, E3, E4 };

var sups = DeterministicFiniteAutomaton.LocalModularReducedSupervisor(plants,specs).ToArray();
//var sups = new[] { DeterministicFiniteAutomaton.MonolithicSupervisor(plants, specs, true) };

var S = sups[0];

S.showAutomaton("Sup");


var events = new[] { ep, cg, cp, sp_r, sp_f, see_f, see_r, sse_r, sed_r, sed_f, ssd_r, ee, ed, cf, ce, cd, spos };

var plc = new PLCprogram(sups, plants, events);
plc.ToXMLFile("SDiM.xml");
