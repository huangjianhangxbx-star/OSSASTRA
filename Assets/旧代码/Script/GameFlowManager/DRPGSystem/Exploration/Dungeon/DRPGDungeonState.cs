using System;
using System.Collections.Generic;
using System.Linq;

public enum DRPGDungeonEvent { Empty, Battle, Rubble, Treasure, Trap, Scout }
[Serializable] public sealed class DRPGDungeonCell
{
    public DRPGDungeonEvent Kind;
    public bool Known, Visited, Done;
    [NonSerialized] public DRPGLootContainerState Loot = new DRPGLootContainerState();
}
[Serializable] public sealed class DRPGDungeonRoom
{
    public int Id, X, Y;
    public bool Visited, Discovered, Scouted;
}
[Serializable] public sealed class DRPGDungeonCorridor
{
    public int A, B;
    public DRPGDungeonCell[] Cells;
}
/// <summary>Exploration facts only. No battle, inventory, camera or UI authority.</summary>
public sealed class DRPGDungeonState
{
    public readonly List<DRPGDungeonRoom> Rooms = new List<DRPGDungeonRoom>();
    public readonly List<DRPGDungeonCorridor> Corridors = new List<DRPGDungeonCorridor>();
    public int Seed { get; }
    public int Room { get; private set; }
    public int Edge { get; private set; } = -1;
    public int From { get; private set; }
    public float Distance { get; private set; }
    public const float CellSpacing = 4f;
    public float Length => Edge < 0 ? 0f : (Corridors[Edge].Cells.Length + 1) * CellSpacing;
    public int Revision { get; private set; }
    public int VisitedCount => Rooms.Count(r => r.Visited);
    public bool AtDoor => Edge >= 0 && (Distance <= .03f || Distance >= Length - .03f);
    public DRPGDungeonState(int seed, bool testRoute = false)
    {
        Seed = seed;
        var random = new Random(seed);
        int[,] initial = {{0,0},{1,0},{1,1},{0,1}};
        for(int i=0;i<4;i++) Rooms.Add(new DRPGDungeonRoom {Id=i,X=initial[i,0],Y=initial[i,1]});
        AddEdge(0,1,random); AddEdge(1,2,random); AddEdge(2,3,random); AddEdge(3,0,random);
        while(Rooms.Count<10)
        {
            var a=Rooms[random.Next(Rooms.Count)]; int dir=random.Next(4);
            int x=a.X+(dir==0?1:dir==1?-1:0), y=a.Y+(dir==2?1:dir==3?-1:0);
            if(Rooms.Any(r=>r.X==x&&r.Y==y)) continue;
            int id=Rooms.Count; Rooms.Add(new DRPGDungeonRoom{Id=id,X=x,Y=y}); AddEdge(a.Id,id,random);
        }
        if(testRoute) {
            var kinds=new[]{DRPGDungeonEvent.Empty,DRPGDungeonEvent.Rubble,DRPGDungeonEvent.Treasure,DRPGDungeonEvent.Scout,DRPGDungeonEvent.Battle};
            Corridors[0].Cells=kinds.Select(k=>new DRPGDungeonCell{Kind=k}).ToArray();
        }
        Visit(0);
    }
    void AddEdge(int a,int b,Random random)
    {
        var cells=new DRPGDungeonCell[random.Next(3,6)];
        for(int i=0;i<cells.Length;i++) { int roll=random.Next(100); cells[i]=new DRPGDungeonCell{Kind=roll<18?DRPGDungeonEvent.Battle:roll<30?DRPGDungeonEvent.Rubble:roll<45?DRPGDungeonEvent.Treasure:roll<55?DRPGDungeonEvent.Trap:roll<63?DRPGDungeonEvent.Scout:DRPGDungeonEvent.Empty}; }
        Corridors.Add(new DRPGDungeonCorridor{A=a,B=b,Cells=cells});
    }
    void Visit(int room)
    {
        Room=room; Rooms[room].Visited=Rooms[room].Discovered=true;
        foreach(var e in Corridors) if(e.A==room||e.B==room) {Rooms[e.A].Discovered=true;Rooms[e.B].Discovered=true;}
        Revision++;
    }
    public bool Visible(DRPGDungeonCorridor e) => Rooms[e.A].Visited||Rooms[e.B].Visited||e.Cells.Any(c=>c.Known);
    public bool Travel(int to)
    {
        if(Edge>=0) return false;
        int edge=Corridors.FindIndex(e=>(e.A==Room&&e.B==to)||(e.B==Room&&e.A==to));
        if(edge<0) return false;
        From=Room;Edge=edge;Distance=0;Revision++;return true;
    }
    public float CellDistance(int index) => (From==Corridors[Edge].A ? index+1 : Corridors[Edge].Cells.Length-index)*CellSpacing;
    public DRPGDungeonCell Move(float requested)
    {
        if(Edge<0||float.IsNaN(requested)||float.IsInfinity(requested)) return null;
        float target=Math.Max(0,Math.Min(Length,requested)); var edge=Corridors[Edge];
        var crossed=Enumerable.Range(0,edge.Cells.Length).OrderBy(i=>Math.Abs(CellDistance(i)-Distance));
        foreach(int i in crossed) {
            var c=edge.Cells[i]; float x=CellDistance(i);
            bool passing=target>Distance ? x>Distance&&x<=target : x<Distance&&x>=target;
            bool blocked=!c.Done&&(c.Kind==DRPGDungeonEvent.Rubble||c.Kind==DRPGDungeonEvent.Trap||c.Kind==DRPGDungeonEvent.Battle)&&target>Distance&&Math.Abs(x-Distance)<.02f;
            if(blocked){Distance=x;return c;}
            if(!passing) continue;
            c.Known=c.Visited=true;
            if(c.Kind==DRPGDungeonEvent.Empty) c.Done=true;
            Revision++;
            if(!c.Done||!c.Loot.IsEmpty){Distance=x;return c;}
        }
        Distance=target; return null;
    }
    public bool EnterDoor()
    {
        if(!AtDoor) return false;
        var e=Corridors[Edge]; int to=Distance<Length*.5f?From:(From==e.A?e.B:e.A);
        Edge=-1;Distance=0;Visit(to);return true;
    }
    public void Scout()
    {
        int id=Edge<0?Room:From;Rooms[id].Scouted=true;
        foreach(var e in Corridors) if(e.A==id||e.B==id) {Rooms[e.A].Discovered=Rooms[e.B].Discovered=true;foreach(var c in e.Cells)c.Known=true;}
        Revision++;
    }
    public void Complete(DRPGDungeonCell cell){if(cell==null||cell.Done)return;cell.Done=true;Revision++;}
}
