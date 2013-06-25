#include "GridCutLib.h"

Grid* __grid;

DLLEXPORT Grid* __stdcall CreateGraph(int w, int h)
{
	__grid = new Grid(w, h);
	return __grid;
}
DLLEXPORT void __stdcall SetEdgeWeights(Grid* grid, int w, int h, double* Vv, double* Vh)
{
	for (int y = 0; y < h; y++)
	{
		for (int x = 0; x < w - 1; x++)
		{
			int i = x + y * w;
			__grid->set_neighbor_cap(__grid->node_id(x, y), +1, 0, Vv[i]);
			__grid->set_neighbor_cap(__grid->node_id(x + 1, y), -1, 0, Vv[i]);
		}
	}
	for (int y = 0; y < h - 1; y++)
	{
		for (int x = 0; x < w; x++)
		{
			int i = x + y * w;
			__grid->set_neighbor_cap(__grid->node_id(x, y), 0, +1, Vh[i]);
			__grid->set_neighbor_cap(__grid->node_id(x, y + 1), 0, -1, Vh[i]);
		}
	}
}

DLLEXPORT void __stdcall Labelling(Grid* grid, int labelID, int w, int h, double* D, int* S, int* L)
{
	// TODO: 
	for (int y = 0; y < h; y++)
	{
		for (int x = 0; x < w; x++)
		{
			int strokeID = S[x + y * w];
			double d = D[x + y * w];
			if (strokeID == labelID)
			{
				grid->set_terminal_cap(grid->node_id(x, y), d, 0);
			}
			else if (strokeID > labelID)
			{
				grid->set_terminal_cap(grid->node_id(x, y), 0, d);
			}
			else
			{
//				grid->set_terminal_cap(grid->node_id(x, y), 0, 0);
			}
		}
	}
	
	grid->compute_maxflow();

	for (int y = 0; y < h; y++)
	{
		for (int x = 0; x < w; x++)
		{
			int i = x + y * w;
			// ÊFÏ‚Ý‚Å‚È‚¢
			if (L[i] < 0 && __grid->get_segment(grid->node_id(x, y)) == 0)
			{
				L[i] = labelID;
			}
		}
	}
}

DLLEXPORT void __stdcall DeleteGraph(Grid* grid)
{
	delete grid;
}
