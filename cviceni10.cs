using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
  struct Graph
  {
    public int VertexCount;
    public Dictionary<(int, int), double> Edges;
  }

  class FloydWarshallTD
  {
    private Dictionary<(int, int, int), double> memoized = new Dictionary<(int, int, int), double>();
    private Graph graph;

    public FloydWarshallTD(Graph g) => graph = g;

    private double Step(int from, int to, int via)
    {
      if (from == to) return 0.0;
      if (via == 0) return graph.Edges.TryGetValue((from, to), out double w) ? w : double.PositiveInfinity;

      var key = (from, to, via);
      if (memoized.ContainsKey(key))
      {
        return memoized[key];
      }

      double result = Math.Min(Step(from, to, via - 1), Step(from, via - 1, via - 1) + Step(via - 1, to, via - 1));
      memoized[key] = result;
      return result;
    }

    public double Shortest(int from, int to) => Step(from, to, graph.VertexCount);
  }

  class FloydWarshallBU
  {
    private Graph graph;
    private double[,] paths;
    private int[,] next;

    public FloydWarshallBU(Graph g) => graph = g;

    private void ComputePaths()
    {
      paths = new double[graph.VertexCount, graph.VertexCount];
      next = new int[graph.VertexCount, graph.VertexCount];

      for (int i = 0; i < graph.VertexCount; i++)
      {
        for (int j = 0; j < graph.VertexCount; j++)
        {
          if (i == j)
          {
            paths[i, j] = 0.0;
            next[i, j] = j;
          }
          else if (graph.Edges.TryGetValue((i, j), out double w))
          {
            paths[i, j] = w;
            next[i, j] = j;
          }
          else
          {
            paths[i, j] = double.PositiveInfinity;
            next[i, j] = -1;
          }
        }
      }

      for (int k = 0; k < graph.VertexCount; k++)
      {
        for (int i = 0; i < graph.VertexCount; i++)
        {
          for (int j = 0; j < graph.VertexCount; j++)
          {
            double candidate = paths[i, k] + paths[k, j];
            if (candidate < paths[i, j])
            {
              paths[i, j] = candidate;
              next[i, j] = next[i, k];
            }
          }
        }
      }
    }

    public double Shorest(int from, int to)
    {
      if (paths == null) ComputePaths();
      return paths[from, to];
    }

    public List<int> Path(int from, int to)
    {
      if (next == null) ComputePaths();
      if (next[from, to] == -1) return null;
      List<int> result = new List<int>();
      result.Add(from);

      while (from != to)
      {
        from = next[from, to];
        result.Add(from);
      }

      return result;
    }
  }

  class Program
  {
    static void Main(string[] args)
    {
      Graph g = new Graph()
      {
        VertexCount = 3,
        Edges = new Dictionary<(int, int), double>()
        {
          { (0, 1), 1.0 },
          { (1, 2), 1.0 },
          { (2, 0), 1.0 }
        }
      };

      FloydWarshallBU fw = new FloydWarshallBU(g);
      for (int i = 0; i < g.VertexCount; i++)
      {
        for (int j = 0; j < g.VertexCount; j++)
        {
          Console.WriteLine($"{i} {j} {fw.Shorest(i, j)}");
        }
      }

      var path = fw.Path(2, 1);
      if (path == null)
      {
        Console.WriteLine("No path exists");
      }
      else
      {
        Console.Write("Path: ");
        foreach (var i in path)
        {
          Console.Write($"{i} ");
        }
        Console.WriteLine();
      }
    }
  }
}
