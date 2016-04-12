﻿#version 330


// Texture dimensions in px, tiles per row
uniform ivec3	texSizeCount	= ivec3(256,256, 16);
// Tile size, tile margin in px
uniform ivec4	tileSizeMargin	= ivec4(16,16, 0,0);


layout(location=0) in vec2	v_position;
layout(location=1) in int	v_texOffset;

out vec2 f_texCoods;


vec2 GetTexCoods()
{
	// Tile positions
    ivec2 off = ivec2(v_texOffset % texSizeCount.z, v_texOffset / texSizeCount.z);
	// Texture positions (top-left)
	ivec2 uv = off * (tileSizeMargin.xy + tileSizeMargin.zw);

	// Offset the vertex according to its position in the quad
	switch (gl_VertexID % 4)
	{
		case 0:
		break;

		case 1:
		uv += ivec2(tileSizeMargin.x, 0);
		break;
				
		case 2:
		uv += ivec2(tileSizeMargin.x, tileSizeMargin.y);
		break;
				
		case 3:
		uv += ivec2(0, tileSizeMargin.y);
		break;
	}

	// Normalize to tex coordinates
	return vec2(uv) / texSizeCount.xy;
}

void main()
{
	f_texCoods = GetTexCoods();
	gl_Position = vec4(v_position, 0, 1);
}
