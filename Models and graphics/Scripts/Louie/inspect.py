import bpy, json, sys
out = sys.argv[sys.argv.index("--")+1]
r={}
r['objects']=[(o.name,o.type,o.parent.name if o.parent else None,[m.name for m in o.material_slots] if o.type=='MESH' else None, [md.type+":"+md.name for md in o.modifiers] if o.type=='MESH' else None) for o in bpy.data.objects]
r['images']=[(i.name,i.filepath,tuple(i.size),i.packed_file is not None) for i in bpy.data.images]
mats={}
for m in bpy.data.materials:
    if m.use_nodes:
        mats[m.name]=[(n.type,n.name,n.image.name if getattr(n,'image',None) else None) for n in m.node_tree.nodes]
r['mats']=mats
r['texts']=[t.name for t in bpy.data.texts]
r['actions']=[a.name for a in bpy.data.actions]
r['collections']=[(c.name,[o.name for o in c.objects]) for c in bpy.data.collections]
json.dump(r,open(out,'w'),indent=1)
