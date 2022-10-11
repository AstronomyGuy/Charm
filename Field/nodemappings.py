from codecs import namereplace_errors
import bpy # for convenience w/ intellij
GUIDE:
§name is the unique identifier for the node
§var is the variable being set in the operation
§p[n] is the nth parameter of the operation, e.g. §p0 is the first parameter
Functions are split by a newline followed by "## "
## add
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'ADD'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§var = §name.outputs[0]
## and
## atomic_and
## atomic_cmp_store
## atomic_iadd
## atomic_imax
## atomic_imin
## atomic_or
## atomic_umax
## atomic_umin
## atomic_xor
## bfi
## bfrev
## bufinfo
## countbits
## cut
## cut_stream
## dadd
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'ADD'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§var = §name.outputs[0]
## ddiv
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'DIVIDE'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§var = §name.outputs[0]
## deq
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'COMPARE'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§name.inputs[2].default_value = 0.0001
§var = §name.outputs[0]
## deriv_rtx_coarse
## deriv_rtx_fine
## deriv_rty_coarse
## deriv_rty_fine
## dfma
## dge
## div
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'DIVIDE'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§var = §name.outputs[0]
## dlt
## dmax
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'MAXIMUM'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§var = §name.outputs[0]
## dmin
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'MINIMUM'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§var = §name.outputs[0]
## dmov
§var = §p0
## dmovc
§name = matnodes.new("ShaderNodemMixRGB")
§name.blend_type = 'VALUE'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§name.inputs[2] = §p2
§var = §name.outputs[0]
## dmul
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'MULTIPLY'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§var = §name.outputs[0]
## dne
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'COMPARE'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§name.inputs[2].default_value = 0.0001
§var = §name.outputs[0]
## dp2
## dp3
## dp4
## drcp
## eq
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'COMPARE'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§name.inputs[2].default_value = 0.001
§var = §name.outputs[0]
## exp
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'EXPONENT'
§name.inputs[0] = §p0
§var = §name.outputs[0]
## f16tof32
## f32tof16
## fcall
## firstbit
## frc
## ftod
## ftoi
## ftou
## gather4
## gather4_c
## gather4_po
## gather4_po_c
## ge
§name_cmp = matnodes.new("ShaderNodeMath")
§name_cmp.operation = 'COMPARE'
§name_cmp.inputs[0] = §p0
§name_cmp.inputs[1] = §p1
§name_cmp.inputs[2].default_value = 0.001

§name_gt = matnodes.new("ShaderNodeMath")
§name_gt.operation = 'GREATER_THAN'
§name_gt.inputs[0] = §p0
§name_gt.inputs[1] = §p1

§name_or = matnodes.new("ShaderNodeMath")
§name_or.operation = 'ADD'
§name_or.inputs[0] = §name_gt.outputs[0]
§name_or.inputs[1] = §name_cmp.outputs[0]

§name = matnodes.new("ShaderNodeMath")
§name.operation = 'GREATER_THAN'
§name.inputs[0] = §name_cmp.outputs[0]
§name.inputs[1].default_value = 0.5

§var = §name.outputs[0]
## hs_control_point_phase
## hs_decls
## hs_fork_phase
## hs_join_phase
## iadd
§name_r1 = matnodes.new("ShaderNodeMath")
§name_r1.operation = 'FLOOR'
§name_r1.inputs[0] = §p0

§name_r2 = matnodes.new("ShaderNodeMath")
§name_r2.operation = 'FLOOR'
§name_r2.inputs[0] = §p1

§name = matnodes.new("ShaderNodeMath")
§name.operation = 'ADD'
§name.inputs[0] = §name.outputs[0]
§name.inputs[1] = §name.outputs[1]

§var = §name.outputs[0]
## ibfe
## ieq
§name_r1 = matnodes.new("ShaderNodeMath")
§name_r1.operation = 'FLOOR'
§name_r1.inputs[0] = §p0

§name_r2 = matnodes.new("ShaderNodeMath")
§name_r2.operation = 'FLOOR'
§name_r2.inputs[0] = §p1

§name = matnodes.new("ShaderNodeMath")
§name.operation = 'COMPARE'
§name.inputs[0] = §name_r1.outputs[0]
§name.inputs[1] = §name_r2.outputs[1]
§name.inputs[2].default_value = 0.001

§var = §name.outputs[0]
## ige
§name_r1 = matnodes.new("ShaderNodeMath")
§name_r1.operation = 'FLOOR'
§name_r1.inputs[0] = §p0

§name_r2 = matnodes.new("ShaderNodeMath")
§name_r2.operation = 'FLOOR'
§name_r2.inputs[0] = §p1

§name_cmp = matnodes.new("ShaderNodeMath")
§name_cmp.operation = 'COMPARE'
§name_cmp.inputs[0] = §name_r1
§name_cmp.inputs[1] = §name_r2
§name_cmp.inputs[2].default_value = 0.001

§name_gt = matnodes.new("ShaderNodeMath")
§name_gt.operation = 'GREATER_THAN'
§name_gt.inputs[0] = §name_r1
§name_gt.inputs[1] = §name_r2

§name_or = matnodes.new("ShaderNodeMath")
§name_or.operation = 'ADD'
§name_or.inputs[0] = §name_gt.outputs[0]
§name_or.inputs[1] = §name_cmp.outputs[0]

§name = matnodes.new("ShaderNodeMath")
§name.operation = 'GREATER_THAN'
§name.inputs[0] = §name_cmp.outputs[0]
§name.inputs[1].default_value = 0.5

§var = §name.outputs[0]
## ilt
§name_r1 = matnodes.new("ShaderNodeMath")
§name_r1.operation = 'FLOOR'
§name_r1.inputs[0] = §p0

§name_r2 = matnodes.new("ShaderNodeMath")
§name_r2.operation = 'FLOOR'
§name_r2.inputs[0] = §p1

§name = matnodes.new("ShaderNodeMath")
§name.operation = 'LESS_THAN'
§name.inputs[0] = §name_r1.outputs[0]
§name.inputs[1] = §name_r2.outputs[1]

§var = §name.outputs[0]
## imad
§name_r1 = matnodes.new("ShaderNodeMath")
§name_r1.operation = 'FLOOR'
§name_r1.inputs[0] = §p0

§name_r2 = matnodes.new("ShaderNodeMath")
§name_r2.operation = 'FLOOR'
§name_r2.inputs[0] = §p1

§name_r3 = matnodes.new("ShaderNodeMath")
§name_r3.operation = 'FLOOR'
§name_r3.inputs[0] = §p2

§name = matnodes.new("ShaderNodeMath")
§name.operation = 'MULTIPLY_ADD'
§name.inputs[0] = §name_r1.outputs[0]
§name.inputs[1] = §name_r2.outputs[1]
§name.inputs[2] = §name_r3.outputs[2]

§var = §name.outputs[0]
## imin
§name_r1 = matnodes.new("ShaderNodeMath")
§name_r1.operation = 'FLOOR'
§name_r1.inputs[0] = §p0

§name_r2 = matnodes.new("ShaderNodeMath")
§name_r2.operation = 'FLOOR'
§name_r2.inputs[0] = §p1

§name = matnodes.new("ShaderNodeMath")
§name.operation = 'MINIMUM'
§name.inputs[0] = §name_r1.outputs[0]
§name.inputs[1] = §name_r2.outputs[1]

§var = §name.outputs[0]
## imm_atomic_alloc
## imm_atomic_and
## imm_atomic_cmp_exch
## imm_atomic_consume
## imm_atomic_exch
## imm_atomic_iadd
## imm_atomic_imax
## imm_atomic_imin
## imm_atomic_or
## imm_atomic_umax
## imm_atomic_umin
## imm_atomic_xor
## imul
§name_r1 = matnodes.new("ShaderNodeMath")
§name_r1.operation = 'FLOOR'
§name_r1.inputs[0] = §p0

§name_r2 = matnodes.new("ShaderNodeMath")
§name_r2.operation = 'FLOOR'
§name_r2.inputs[0] = §p1

§name = matnodes.new("ShaderNodeMath")
§name.operation = 'MULTIPLY'
§name.inputs[0] = §name_r1.outputs[0]
§name.inputs[1] = §name_r2.outputs[1]

§var = §name.outputs[0]
## ine
§name_cmp = matnodes.new("ShaderNodeMath")
§name_cmp.operation = 'COMPARE'
§name_cmp.inputs[0] = §p0
§name_cmp.inputs[1] = §p1
§name_cmp.inputs[2].default_value = 0.001

§name = matnodes.new("ShaderNodeMath")
§name.operation = 'SUBTRACT'
§name.inputs[0].default_value = 1.0
§name.inputs[1] = §name_cmp.outputs[0]
§var = §name.outputs[0]
## ineg
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'MULTIPLY'
§name.inputs[0] = §p0
§name.inputs[1].default_value = -1.0
§var = §name.outputs[0]
## ishl
## ishr
## itof
#converts int to float, happens implicitly in blender (§p0)
§var = §p0
## ld
§var = §p1
## ld_raw
## ld_structured
## ld_uav_typed
## ld2dms
## lod
## log
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'LOGARITHM'
§name.inputs[0] = §p0
§name.inputs[1].default_value = 2.0
§var = §name.outputs[0]
## lt
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'LESS_THAN'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§var = §name.outputs[0]
## mad
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'MULTIPLY_ADD'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§name.inputs[2] = §p2
§var = §name.outputs[0]
## max
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'MAXIMUM'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§var = §name.outputs[0]
## min
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'MINIMUM'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§var = §name.outputs[0]
## mov
§var = §p0
## movc
§name = matnodes.new("ShaderNodemMixRGB")
§name.blend_type = 'VALUE'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§name.inputs[2] = §p2
§var = §name.outputs[0]
## mul
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'MULTIPLY'
§name.inputs[0] = §p0
§name.inputs[1] = §p1
§var = §name.outputs[0]
## ne
§name_cmp = matnodes.new("ShaderNodeMath")
§name_cmp.operation = 'COMPARE'
§name_cmp.inputs[0] = §p0
§name_cmp.inputs[1] = §p1
§name_cmp.inputs[2].default_value = 0.001

§name = matnodes.new("ShaderNodeMath")
§name.operation = 'SUBTRACT'
§name.inputs[0].default_value = 1.0
§name.inputs[1] = §name_cmp.outputs[0]
§var = §name.outputs[0]
## nop
#opcode nop; literally do nothing, why is this a thing
## not
## or
## rcp
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'DIVIDE'
§name.inputs[0].default_value = 1.0
§name.inputs[1] = §name.outputs[0]
§var = §name.outputs[0]
## resinfo
## round_ne
§name_mod = matnodes.new("ShaderNodeMath")
§name_mod.operation = 'MODULO'
§name_mod.inputs[0] = §p0
§name.inputs[1].default_value = 2.0

§name_subt = matnodes.new("ShaderNodeMath")
§name_subt.operation = 'SUBTRACT'
§name_subt.inputs[0] = §name_mod.outputs[0]
§name_subt.inputs[1].default_value = 1.0

§name_div = matnodes.new("ShaderNodeMath")
§name_div.operation = 'DIVIDE'
§name_div.inputs[0] = §name_subt.outputs[0]
§name_div.inputs[1].default_value = 1000.0

§name_add = matnodes.new("ShaderNodeMath")
§name_add.operation = 'ADD'
§name_add.inputs[0] = §p0
§name_add.inputs[0] = §name_div.outputs[0]

§name = matnodes.new("ShaderNodeMath")
§name.operation = 'ROUND'
§name.inputs[0] = §name_add
§var = §name.outputs[0]
## round_ni
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'FLOOR'
§name.inputs[0] = §p0
§var = §name.outputs[0]
## round_pi
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'CEIL'
§name.inputs[0] = §p0
§var = §name.outputs[0]
## round_z
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'TRUNC'
§name.inputs[0] = §p0 
§var = §name.outputs[0]
## rsq
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'INVERSE_SQRT'
§name.inputs[0] = §p0
§var = §name.outputs[0]
## sample
§var = §p1
## sample_b
## sample_c
## sample_c_lz
## sample_d
## sample_l
## sampleinfo
## samplepos
## sincos
§name_sin = matnodes.new("ShaderNodeMath")
§name_sin.operation = 'SINE'
§name_sin.inputs[0] = §p1
§var = §name_sin.outputs[0]

§name_cos = matnodes.new("ShaderNodeMath")
§name_cos.operation = 'COSINE'
§name_cos.inputs[0] = §p1
§p0 = §name_cos.outputs[0]
## sqrt
§name = matnodes.new("ShaderNodeMath")
§name.operation = 'SQRT'
§name.inputs[0] = §p0
§var = §name.outputs[0]
## store_raw
## store_structured
## store_uav_typed
## swapc
## sync
#sync opcode; probably hopefully not relevant to blender
## uaddc
## ubfe
## udiv
## uge
## ult
## umad
## umax
## umin
## umul
## ushr
## usubb
## utof
## xor