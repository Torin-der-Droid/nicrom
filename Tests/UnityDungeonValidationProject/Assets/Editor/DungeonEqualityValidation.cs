using System;
using System.Collections.Generic;
using UnityEngine;
public static class DungeonEqualityValidation {
static void Check(bool ok) { if(!ok) throw new Exception("Equality contract failed"); }
public static void Run() {
var a=Vector3.zero; var b=Vector3.right; var c=Vector3.forward;
var e=new Edge(a,b); var er=new Edge(b,a);
Check(e==er && e.Equals((object)er) && e.GetHashCode()==er.GetHashCode());
Check(new HashSet<Edge>{e}.Contains(er));
var t=new Triangle(a,b,c); var tr=new Triangle(c,a,b);
Check(t==tr && t.Equals((object)tr) && t.GetHashCode()==tr.GetHashCode());
Check(t!=null && !t.Equals((object)null) && (Triangle)null==(Triangle)null);
var r=new Room{center=b}; var rr=new Room{center=b};
Check(r==rr && r.Equals((object)rr) && r.GetHashCode()==rr.GetHashCode());
var n=new AStarNode(Vector3Int.one,2,3); var nn=new AStarNode(Vector3Int.one,3,2);
Check(n==nn && n.Equals((object)nn) && n.GetHashCode()==nn.GetHashCode());
Check(!e.Equals((object)t) && !r.Equals((object)n));
System.IO.File.WriteAllText("Logs/DungeonEquality.txt","PASS: boxed equality, null, unordered vertices and matching hashes");
} }
