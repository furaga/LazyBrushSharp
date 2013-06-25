#ifdef __cplusplus
#define DLLEXPORT extern "C" __declspec(dllexport)
#else
#define DLLEXPORT __declspec(dllexport)
#endif

#include "GridGraph_2D_4C.h"

typedef GridGraph_2D_4C<double,double,double> Grid;

DLLEXPORT Grid* __stdcall CreateGraph(int w, int h);
DLLEXPORT void __stdcall SetEdgeWeights(Grid* grid, int w, int h, double* Vv, double* Vh);
DLLEXPORT void __stdcall Labelling(Grid* grid, int labelID, int w, int h, double* D, int* S, int* L);
DLLEXPORT void __stdcall DeleteGraph(Grid* grid);
