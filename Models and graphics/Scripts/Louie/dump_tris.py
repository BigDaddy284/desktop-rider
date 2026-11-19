"""Dumps the rider's (or bike's) UV triangles with their rest-pose world positions and face normals (read only).
    -- <object name> <out .npz>"""
import bpy, sys
import numpy as np
name, out = sys.argv[sys.argv.index("--") + 1:][:2]
sc = bpy.context.scene; sc.frame_set(1)
ob = bpy.data.objects[name]; ev = ob.evaluated_get(bpy.context.evaluated_depsgraph_get()); me = ev.to_mesh()
M = ev.matrix_world; N = M.to_3x3().inverted().transposed()
uvl = me.uv_layers[0].data; mats = [s.name for s in ob.material_slots]
U, P, NR, MI = [], [], [], []
for p in me.polygons:
    n = (N @ p.normal).normalized()
    li = list(p.loop_indices); vi = list(p.vertices)
    for k in range(1, len(li) - 1):
        idx = (0, k, k + 1)
        U.append([list(uvl[li[j]].uv) for j in idx]); P.append([list(M @ me.vertices[vi[j]].co) for j in idx])
        NR.append(list(n)); MI.append(p.material_index)
np.savez_compressed(out, uv=np.array(U, np.float32), pos=np.array(P, np.float32), nrm=np.array(NR, np.float32), mat=np.array(MI, np.int32), names=np.array(mats))
ev.to_mesh_clear(); print("DUMP", len(U))
