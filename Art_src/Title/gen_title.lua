-- SWARM title background generator
local SRC  = "C:/Users/jaehyeon/UnityProjects/Swarm/Art_src/Title/"
local OUTP = "C:/Users/jaehyeon/UnityProjects/Swarm/Assets/_Project/Textures/Title/"
local W, H = 480, 360

local spr = Sprite(W, H, ColorMode.RGB)
local img = spr.cels[1].image
spr.filename = SRC .. "Title_Dawn.aseprite"

local function rgb(h) return app.pixelColor.rgba((h>>16)&0xFF,(h>>8)&0xFF,h&0xFF,255) end
local seed = 20260902
local function sset(s) seed=s end
local function rnd() seed=(seed*1103515245+12345)%2147483648; return seed/2147483648 end
local function ri(n) return math.floor(rnd()*n) end
local function P(x,y,c) x=math.floor(x); y=math.floor(y)
  if x>=0 and y>=0 and x<W and y<H then img:putPixel(x,y,rgb(c)) end end
local BAYER={{0,8,2,10},{12,4,14,6},{3,11,1,9},{15,7,13,5}}
local function bay(x,y) return BAYER[(math.floor(y)%4)+1][(math.floor(x)%4)+1]/16 end
local function mix2(x,y,t,c1,c2) if bay(x,y)<t then return c2 else return c1 end end
local function flat(x,y,v,tbl)
  local n=#tbl; local f=v*(n-1)
  if f<0 then f=0 end; if f>n-1 then f=n-1 end
  local i=math.floor(f); local fr=f-i
  if i>=n-1 then return tbl[n] end
  if fr<0.36 then return tbl[i+1] end
  if fr>0.64 then return tbl[i+2] end
  return mix2(x,y,(fr-0.36)/0.28,tbl[i+1],tbl[i+2])
end
local function hsh(i) local v=(math.floor(i)*2654435761)%2147483647; return (v%1000)/1000 end

local C = {
  SKY1=0x3E6C8E, SKY2=0x5A8CA8, SKY3=0x86B4C4, SKY4=0xB8D6D2, SKY5=0xE8D8B8,
  SUN=0xFFF4D2, SUNC=0xFFFDF0, MIST=0xD8DEC8,
  FAR1=0x93AFBA, FAR2=0x7E9CAA, HILL1=0x63878A, HILL2=0x527478,
  G_HI=0xA8B87A, G_LIT=0x7E9A5E, G_MID=0x56784A, G_SHD=0x3A5A44, G_DRK=0x2A4038,
  R_HI=0xD6CCB2, R_LIT=0xB6AC96, R_MID=0x8E897C, R_SHD=0x666A68, R_DRK=0x474E54,
  W_HI=0xF0DCB0, W_LIT=0xC0B49C, W_MID=0x8E8C82, W_SHD=0x5E6470, W_DRK=0x414A5A, W_VOID=0x2A3442,
  MOSS=0x4E6E52, MOSS2=0x3A5A46,
  L_RIM=0xD8DC8E, L_HI=0x9EB264, L_LIT=0x6E8C4E, L_MID=0x4A6C46, L_SHD=0x32523E, L_DRK=0x223A34,
  TRK_L=0x8A7A5E, TRK=0x53483E, TRK_D=0x2E2A30,
  SIL_D=0x1E2A34, SIL=0x2B3A44, SIL_L=0x3E4E56,
  CLK_D=0x4A2830, CLK=0x6E3A38, CLK_L=0x94514A,
  RIM=0xF0DCB0, RIM2=0xC0A882,
  FG_D=0x1B2A2C, FG=0x273A38, FG_L=0x39514A,
}
local GR={C.G_DRK,C.G_SHD,C.G_MID,C.G_LIT,C.G_HI}
local RR={C.R_DRK,C.R_SHD,C.R_MID,C.R_LIT,C.R_HI}
local WR={C.W_DRK,C.W_SHD,C.W_MID,C.W_LIT}
local LR={C.L_DRK,C.L_SHD,C.L_MID,C.L_LIT,C.L_HI}

local SUNX,SUNY = 396,92
local HZ = 170
local function lightAt(x,y)
  return 0.46 + 0.30*((x/W)-0.42) + 0.16*math.sin(x*0.011+y*0.007)
       + 0.10*math.sin(x*0.019-y*0.013+2.1) + 0.20*((y-HZ)/(H-HZ))
end

-- ===================== SKY =====================
for _,b in ipairs({{0,58,C.SKY1,C.SKY2},{58,106,C.SKY2,C.SKY3},{106,142,C.SKY3,C.SKY4},{142,174,C.SKY4,C.SKY5}}) do
  for y=b[1],b[2]-1 do
    local t=(y-b[1])/(b[2]-b[1])
    for x=0,W-1 do P(x,y, mix2(x,y,t,b[3],b[4])) end
  end
end
for y=0,190 do for x=0,W-1 do
  local dx,dy=x-SUNX,(y-SUNY)/0.92
  local g=1-math.sqrt(dx*dx+dy*dy)/130
  if g>0 then g=g*g
    if bay(x,y)<g*0.95 then P(x,y,C.SKY5) end
    if bay(x+2,y+1)<(g-0.44)*2.4 then P(x,y,C.SUN) end
  end
end end
for y=SUNY-12,SUNY+12 do for x=SUNX-12,SUNX+12 do
  local d=math.sqrt((x-SUNX)^2+(y-SUNY)^2)
  if d<=8 then P(x,y,C.SUNC) elseif d<=12 and bay(x,y)<(12-d)/4 then P(x,y,C.SUN) end
end end
local function streak(cy,x0,x1,th,col,al)
  for x=x0,x1 do
    local t=(x-x0)/(x1-x0); local env=math.sin(t*math.pi); local w=th*env
    for k=-w,w do
      local a=al*env*(1-math.abs(k)/(w+0.5))
      if bay(x,cy+k)<a then P(x,cy+k+2*math.sin(x*0.02),col) end
    end
  end
end
streak(44,30,240,3,C.SKY3,0.8) streak(36,150,380,2,C.SKY3,0.65)
streak(70,250,470,3,C.SKY5,0.75) streak(60,300,470,2,C.SUN,0.5)
streak(104,10,190,2,C.SKY4,0.55) streak(120,240,440,3,C.SKY5,0.6)

-- ===================== DISTANT LAND =====================
for x=0,W-1 do
  local h1=142-12*math.sin(x*0.0098)-6*math.sin(x*0.024+1.2)
  for y=math.floor(h1),178 do P(x,y,(y<h1+9) and C.FAR1 or C.FAR2) end
end
for x=0,W-1 do
  local h2=158-8*math.sin(x*0.015+2.4)-4*math.sin(x*0.038)
  for y=math.floor(h2),184 do P(x,y,(y<h2+7) and C.HILL1 or C.HILL2) end
end

-- ===================== GROUND =====================
for y=HZ,H-1 do for x=0,W-1 do P(x,y, flat(x,y,lightAt(x,y),GR)) end end
for y=176,320 do for x=0,W-1 do
  local edge=24+(y-176)*1.55+16*math.sin(y*0.03)
  if x<edge then
    local t=(edge-x)/edge
    if t>0.12 or bay(x,y)<0.5 then P(x,y, flat(x,y,lightAt(x,y)-0.30,GR)) end
  end
end end

-- ===================== ROAD =====================
local RY0,RY1 = 184,H-1
local rL,rR = {},{}
for y=RY0,RY1 do
  local t=(y-RY0)/(RY1-RY0)
  local cx=229+32*math.sin(t*math.pi*1.5)*(1-0.30*t)
  local hw=15+133*(t^1.25)
  rL[y]=cx-hw; rR[y]=cx+hw
end
for y=RY0,RY1 do
  for x=math.floor(rL[y]),math.floor(rR[y]) do P(x,y, flat(x,y,lightAt(x,y)*0.66+0.34,RR)) end
end
local y=RY0; local course=0
while y<H do
  local t=(y-RY0)/(RY1-RY0)
  local rowh=math.floor(5+16*t)
  for x=math.floor(rL[y]),math.floor(rR[y]) do P(x,y,C.R_SHD) end
  local jw=12+26*t
  local off=(course%2==0) and 0 or jw*0.5
  local jx=rL[y]+off
  while jx<rR[y] do
    for k=1,rowh-1 do
      local yy=y+k
      if yy<H and jx>(rL[yy] or 0) and jx<(rR[yy] or 0) then P(jx,yy,C.R_SHD) end
    end
    jx=jx+jw
  end
  y=y+rowh; course=course+1
end
for i=1,16 do
  local yy=RY0+ri(H-RY0); local xx=rL[yy]+rnd()*(rR[yy]-rL[yy])
  local dir=math.pi*0.5+(rnd()-0.5)*2.2
  for k=1,6+ri(16) do
    dir=dir+(rnd()-0.5)*0.55
    xx=xx+math.cos(dir); yy=yy+math.sin(dir)
    if yy<RY0 or yy>H-1 then break end
    if xx>(rL[math.floor(yy)] or 0) and xx<(rR[math.floor(yy)] or 0) then P(xx,yy,C.R_DRK) end
  end
end
for i=1,60 do
  local yy=RY0+8+ri(H-RY0-12)
  local t=(yy-RY0)/(RY1-RY0)
  local xx=rL[yy]+4+rnd()*(rR[yy]-rL[yy]-8)
  local lit=lightAt(xx,yy)>0.66
  for j=1,2+ri(4) do
    local ox=xx+(rnd()-0.5)*7
    for k=0,math.floor(1+4*t) do P(ox,yy-k, lit and C.G_LIT or C.G_SHD) end
  end
end
for yy=RY0,H-1 do
  local t=(yy-RY0)/(RY1-RY0); local n=math.floor(1+4*t)
  for k=0,n do
    if hsh(yy*7+k)<0.5 then P(rL[yy]+k,yy, flat(rL[yy]+k,yy,lightAt(rL[yy],yy)-0.12,GR)) end
    if hsh(yy*13+k)<0.5 then P(rR[yy]-k,yy, flat(rR[yy]-k,yy,lightAt(rR[yy],yy)-0.12,GR)) end
  end
end

-- ===================== CASTLE WALL =====================
local BASEY=186
local GAP_L,GAP_R = 190,268
local ZONES={{26,78,138},{118,152,126},{404,452,130}}
local function wallTop(x)
  if x>GAP_L and x<GAP_R then return nil end
  local m=math.floor(x)%16
  local top=(m<10) and 98 or 108
  for _,z in ipairs(ZONES) do
    if x>z[1]-9 and x<z[2]+9 then
      local target=z[3]+math.floor(hsh(x)*4)
      local w=1.0
      if x<z[1] then w=(x-(z[1]-9))/9 end
      if x>z[2] then w=((z[2]+9)-x)/9 end
      local b=top+(target-top)*w
      if b>top then top=b end
    end
  end
  if x>GAP_L-20 and x<=GAP_L then top=top+(BASEY-top)*((x-(GAP_L-20))/20)^1.8 end
  if x>=GAP_R and x<GAP_R+20 then top=top+(BASEY-top)*(((GAP_R+20)-x)/20)^1.8 end
  return top
end
local TW_L,TW_R = 300,346
local function towerTop(x)
  if x<TW_L or x>TW_R then return nil end
  local m=math.floor(x-TW_L)%12
  local top=(m<7) and 56 or 64
  if x>TW_R-16 then top=top+(x-(TW_R-16))*3.0 end
  return top
end
for x=0,W-1 do
  local wt=wallTop(x)
  if wt then
    for yy=math.floor(wt),BASEY do
      local v=0.24+0.46*((x/W)^1.05)+0.07*math.sin(x*0.028+yy*0.02)
      if yy>BASEY-8 then v=v-0.16 end
      P(x,yy, flat(x,yy,v,WR))
    end
  end
  local tt=towerTop(x)
  if tt then
    for yy=math.floor(tt),BASEY do
      local v=0.18+0.42*(((x-TW_L)/(TW_R-TW_L))^0.9)+0.06*math.sin(x*0.03+yy*0.02)
      if yy>BASEY-8 then v=v-0.16 end
      P(x,yy, flat(x,yy,v,WR))
    end
  end
end
local cn=0
for yy=98,BASEY,8 do
  for x=0,W-1 do
    local wt,tt=wallTop(x),towerTop(x)
    if (wt and yy>wt+1) or (tt and yy>tt+1) then P(x,yy,C.W_SHD) end
  end
  local off=(cn%2==0) and 0 or 8
  for x=off,W-1,16 do
    local wt,tt=wallTop(x),towerTop(x)
    for k=1,7 do
      local y2=yy+k
      if y2<=BASEY and ((wt and y2>wt+1) or (tt and y2>tt+1)) then P(x,y2,C.W_SHD) end
    end
  end
  cn=cn+1
end
for yy=92,112 do for xx=316,322 do P(xx,yy,C.W_VOID) end end
for yy=86,93 do local w=math.floor((yy-86)*0.9); for xx=319-w,319+w do P(xx,yy,C.W_VOID) end end
for yy=86,113 do P(315,yy,C.W_DRK); P(323,yy,C.W_LIT) end
for x=0,W-2 do
  local a,b=wallTop(x),wallTop(x+1)
  if a and b and b-a>2 then for yy=math.floor(a),math.floor(b) do P(x,yy,C.W_HI) end end
  if a and not b and x<GAP_L then for yy=math.floor(a),BASEY do P(x,yy,C.W_HI) end end
  local ta,tb=towerTop(x),towerTop(x+1)
  if ta and tb and tb-ta>1 then for yy=math.floor(ta),math.floor(tb) do P(x,yy,C.W_HI) end end
  if ta and not tb then for yy=math.floor(ta),BASEY do P(x,yy,C.W_HI) end end
end
for x=0,W-1 do
  local tt,wt=towerTop(x),wallTop(x)
  if tt then P(x,math.floor(tt),C.W_HI); P(x,math.floor(tt)+1,C.W_LIT)
  elseif wt then
    P(x,math.floor(wt),(x>170) and C.W_HI or C.W_LIT)
    P(x,math.floor(wt)+1,(x>170) and C.W_LIT or C.W_MID)
  end
end
for i=1,50 do
  local x=ri(W); local wt=wallTop(x)
  if wt then
    local yy=wt+5+rnd()*(BASEY-wt-7)
    local rw,rh=2+ri(4),2+ri(5)
    for dy=-rh,rh do for dx=-rw,rw do
      if (dx*dx)/(rw*rw+0.2)+(dy*dy)/(rh*rh+0.2)<=1 and bay(x+dx,yy+dy)<0.8 then
        local w2=wallTop(x+dx)
        if w2 and yy+dy>=w2+2 and yy+dy<=BASEY then
          P(x+dx,yy+dy,(bay(x+dx,yy+dy)<0.42) and C.MOSS or C.MOSS2)
        end
      end
    end end
  end
end
for x=0,W-1 do
  if wallTop(x) then
    local n=2+math.floor(hsh(x*3)*5)
    for k=0,n do if hsh(x*7+k)>0.3 then P(x,BASEY-k,(hsh(x*11+k)<0.4) and C.MOSS or C.MOSS2) end end
  end
end
for i=1,55 do
  local x=ri(W); local yy=BASEY+1+ri(13)
  local rw,rh=1+ri(4),1+ri(3)
  for dy=-rh,rh do for dx=-rw,rw do
    if (dx*dx)/(rw*rw+0.2)+(dy*dy)/(rh*rh+0.2)<=1 then P(x+dx,yy+dy,(dy<0) and C.W_MID or C.W_DRK) end
  end end
end

-- ===================== LIGHT SHAFTS =====================
for _,r in ipairs({{3.58,22,0.34},{3.78,15,0.28},{3.44,10,0.24},{3.98,18,0.22}}) do
  local ang,wid,amp=r[1],r[2],r[3]
  local dx,dy=math.cos(ang),math.sin(ang)
  local px,py=-dy,dx
  for t=0,460 do
    local bx,by=SUNX+dx*t,SUNY+dy*t
    if by>245 then break end
    local fall=1-t/460
    for k=-wid,wid do
      local x,yy=bx+px*k,by+py*k
      local a=amp*fall*(1-math.abs(k)/(wid+1))
      if yy>195 then a=a*math.max(0,1-(yy-195)/50) end
      if bay(x,yy)<a then P(x,yy,(yy<178) and C.SKY5 or C.MIST) end
    end
  end
end
for y=176,216 do for x=0,W-1 do
  local t=1-math.abs(y-186)/34
  if t>0 then
    local a=t*t*0.8*(0.55+0.45*math.sin(x*0.012+1.7))
    if bay(x,y)<a then P(x,y, mix2(x,y,0.5,C.MIST,C.SKY4)) end
  end
end end

-- ===================== TREES =====================
local function tree(bx, by, trunkH, trunkW, cr, sd)
  sset(sd)
  local cy = by - trunkH - cr*0.58
  local sdx, sdy = SUNX-bx, SUNY-cy
  local sl = math.sqrt(sdx*sdx+sdy*sdy); sdx=sdx/sl; sdy=sdy/sl

  -- cast shadow points directly away from the sun
  local shx = (bx > SUNX) and 1 or -1
  for dx=-cr*1.7,cr*1.7 do for dy=-cr*0.34,cr*0.34 do
    local n=(dx/(cr*1.7))^2+(dy/(cr*0.34))^2
    if n<=1 then
      local x,yy = bx+dx+shx*cr*1.25, by+dy+2
      if bay(x,yy)<0.92-n*0.5 then P(x,yy, flat(x,yy,lightAt(x,yy)-0.32,GR)) end
    end
  end end

  -- trunk: taller, tapered, with real branches
  for k=0,trunkH do
    local t=k/trunkH
    local yy=by-k
    local cxx=bx+math.sin(t*1.3)*trunkW*0.9
    local w=math.max(1, math.floor(trunkW*(1-0.34*t)+0.5))
    for x=cxx-w,cxx+w do
      local rel=(x-cxx)/w
      local c=C.TRK
      if rel*(-shx) > 0.30 then c=C.TRK_L elseif rel*(-shx) < -0.30 then c=C.TRK_D end
      P(x,yy,c)
    end
    P(cxx-w-1,yy,C.TRK_D)
  end
  for _,br in ipairs({{0.62,-1,0.62},{0.80,1,0.55},{0.92,-1,0.40}}) do
    local t0,dir,len=br[1],br[2],br[3]
    local x0=bx+math.sin(t0*1.3)*trunkW*0.9
    local y0=by-trunkH*t0
    local steps=math.floor(cr*len)
    for s=0,steps do
      local u=s/steps
      local x=x0+dir*u*cr*0.85
      local yy=y0-u*cr*0.80
      local w=math.max(0, math.floor(trunkW*0.6*(1-u)))
      for k=-w,w do P(x+k,yy,(k*(-shx)>0) and C.TRK or C.TRK_D) end
    end
  end

  -- scalloped canopy: a ring of small lobes over a solid core
  local lobes={}
  local nOut=11+ri(4)
  for i=1,nOut do
    local a=(i-1)/nOut*math.pi*2 + (rnd()-0.5)*0.42
    local rr=cr*(0.58+rnd()*0.24)
    lobes[#lobes+1]={bx+math.cos(a)*rr, cy+math.sin(a)*rr*0.78, cr*(0.24+rnd()*0.15)}
  end
  for i=1,6 do
    local a=rnd()*math.pi*2; local rr=rnd()*cr*0.38
    lobes[#lobes+1]={bx+math.cos(a)*rr, cy+math.sin(a)*rr*0.8, cr*(0.34+rnd()*0.18)}
  end
  local X0,X1 = math.floor(bx-cr*1.6), math.ceil(bx+cr*1.6)
  local Y0,Y1 = math.floor(cy-cr*1.5), math.ceil(cy+cr*1.4)
  local m={}
  for yy=Y0,Y1 do m[yy]={} end
  for _,L in ipairs(lobes) do
    local lx,ly,s=L[1],L[2],L[3]
    for yy=math.floor(ly-s),math.ceil(ly+s) do
      if m[yy] then
        for xx=math.floor(lx-s),math.ceil(lx+s) do
          local dx,dy=xx-lx,(yy-ly)/0.86
          if dx*dx+dy*dy<=s*s then m[yy][xx]=true end
        end
      end
    end
  end
  local function inm(x,y) local r=m[math.floor(y)]; return r and r[math.floor(x)] end

  for yy=Y0,Y1 do
    if m[yy] then
      for xx=X0,X1 do
        if m[yy][xx] then
          local dx,dy=(xx-bx)/cr,(yy-cy)/cr
          local d = dx*sdx+dy*sdy
          local v = 0.34 + 0.62*d
                  + 0.11*math.sin(xx*0.21+yy*0.17)
                  + 0.08*math.sin(xx*0.47-yy*0.36+1.1)
          P(xx,yy, flat(xx,yy,v,LR))
        end
      end
    end
  end
  -- rim only on the outer edge that actually faces the sun
  for yy=Y0,Y1 do
    if m[yy] then
      for xx=X0,X1 do
        if m[yy][xx] then
          -- surface normal from the local mask gradient
          local nx,ny = 0,0
          for oy=-2,2 do for ox=-2,2 do
            if not inm(xx+ox,yy+oy) then nx=nx+ox; ny=ny+oy end
          end end
          local nl=math.sqrt(nx*nx+ny*ny)
          if nl > 0.001 then
            nx=nx/nl; ny=ny/nl
            local d = nx*sdx+ny*sdy
            if d > 0.35 and not inm(xx+sdx*1.7, yy+sdy*1.7) then
              P(xx,yy,C.L_RIM)
            elseif d > 0.25 and not inm(xx+sdx*3.0, yy+sdy*3.0) then
              if bay(xx,yy)<0.7 then P(xx,yy,C.L_HI) end
            elseif d < -0.30 and not inm(xx-sdx*1.7, yy-sdy*1.7) then
              P(xx,yy,C.L_DRK)
            end
          end
        end
      end
    end
  end
end

tree( 42, 254, 40, 5, 26, 1301)
tree( 98, 219, 26, 3, 16, 2311)
tree( 14, 312, 48, 6, 31, 3313)
tree(448, 266, 46, 6, 29, 4327)
tree(402, 226, 28, 4, 18, 5333)

-- WARRIOR: removed at request
if false then
-- ===================== WARRIOR =====================
local fx,fy = 150,318
local g={}
local function S(x,y,c) x=math.floor(x); y=math.floor(y)
  if x<0 or y<0 or x>=W or y>=H then return end
  g[y]=g[y] or {}; g[y][x]=c end
local function ellS(cx,cy,rw,rh,c)
  for dy=-math.ceil(rh),math.ceil(rh) do for dx=-math.ceil(rw),math.ceil(rw) do
    if (dx*dx)/(rw*rw+0.2)+(dy*dy)/(rh*rh+0.2)<=1.0 then S(cx+dx,cy+dy,c) end
  end end
end
sset(9911)
for dx=-52,10 do for dy=-4,4 do
  local n=(dx/52)^2+(dy/4)^2
  if n<=1 and bay(fx+dx,fy+dy)<0.85-n*0.5 then P(fx+dx-16,fy+dy+1,(n<0.5) and C.R_DRK or C.R_SHD) end
end end
local sx=fx+21
for yy=286,fy+3 do
  local w=(yy>fy-4) and 0 or 1
  for x=sx-w,sx+w do S(x,yy,C.SIL) end
end
for x=sx-7,sx+7 do for yy=283,285 do S(x,yy,C.SIL) end end
for yy=272,282 do for x=sx-1,sx+1 do S(x,yy,C.SIL_D) end end
ellS(sx,270,2.5,2,C.SIL)
for yy=302,fy do
  for x=fx-8,fx-3 do S(x,yy,C.SIL_D) end
  for x=fx+2,fx+7 do S(x,yy,C.SIL_D) end
end
for x=fx-9,fx-2 do S(x,fy,C.SIL_D); S(x,fy+1,C.SIL_D) end
for x=fx+1,fx+8 do S(x,fy,C.SIL_D); S(x,fy+1,C.SIL_D) end
for yy=274,306 do
  local t=(yy-274)/32
  local hw=math.floor(11+7*(t^0.8))
  for x=fx-hw,fx+hw do
    local rel=(x-fx)/hw
    local c=C.CLK
    if rel<-0.45 then c=C.CLK_L end
    if rel>0.35 then c=C.CLK_D end
    if t>0.85 then c=C.CLK_D end
    S(x,yy,c)
  end
end
for x=fx-19,fx+19 do
  local yy=306+math.floor(2*math.sin(x*0.9))
  S(x,yy,C.CLK_D); S(x,yy+1,C.CLK_D)
end
ellS(fx-11,276,6.5,4.5,C.SIL)
ellS(fx+11,276,6.5,4.5,C.SIL)
ellS(fx-13,274,4,2.5,C.SIL_L)
for yy=268,276 do local w=math.floor(5-(yy-268)*0.2); for x=fx-w,fx+w do S(x,yy,C.SIL) end end
ellS(fx,265,6,7,C.SIL)
ellS(fx-2,262,3.5,3.5,C.SIL_L)
ellS(fx+1,255,3.5,5.5,C.CLK)
ellS(fx,253,2,3,C.CLK_L)
for yy=240,fy+2 do
  local row=g[yy]
  if row then
    for x=0,W-1 do
      local c=row[x]
      if c then
        local rightEmpty=not (g[yy] and g[yy][x+1])
        local upEmpty=not (g[yy-1] and g[yy-1][x])
        if rightEmpty then c=C.RIM
        elseif upEmpty and x>fx-6 then c=C.RIM
        elseif upEmpty then c=C.RIM2 end
        P(x,yy,c)
      end
    end
  end
end

end
-- ===================== FOREGROUND =====================
sset(5511)
local function blade(x,base,hgt,lean,col,tip)
  for k=0,hgt do
    local t=k/hgt
    P(x+lean*(t*t)*hgt*0.35, base-k, (k>hgt-3) and tip or col)
  end
end
for i=1,150 do
  local x=-4+rnd()*118
  local w=1-math.max(0,(x-40)/78)
  local base=H-1-ri(6)
  local hgt=math.floor((14+rnd()*30)*(0.35+0.65*w))
  if hgt>3 then
    local lean=(rnd()<0.5) and -1 or 1
    local col=(rnd()<0.25) and C.FG or C.FG_D
    blade(x,base,hgt,lean,col,(rnd()<0.3) and C.FG_L or col)
    if rnd()<0.2 then blade(x+1,base,hgt-2,lean,C.RIM2,C.RIM2) end
  end
end
for i=1,160 do
  local x=362+rnd()*124
  local w=math.min(1,(x-362)/86)
  local base=H-1-ri(6)
  local hgt=math.floor((14+rnd()*32)*(0.35+0.65*w))
  if hgt>3 then
    local lean=(rnd()<0.5) and -1 or 1
    local col=(rnd()<0.28) and C.FG or C.FG_D
    blade(x,base,hgt,lean,col,(rnd()<0.35) and C.FG_L or col)
    if rnd()<0.28 then blade(x+1,base,hgt-2,lean,C.RIM2,C.RIM2) end
  end
end
for x=0,W-1 do
  local hgt=2+math.floor(3*math.abs(math.sin(x*0.19))+2*math.abs(math.sin(x*0.07)))
  for k=0,hgt do P(x,H-1-k,(k>hgt-1) and C.FG or C.FG_D) end
end
local function twig(pts,th0,th1,col)
  local n=#pts
  for i=1,n-1 do
    local a,b=pts[i],pts[i+1]
    local steps=math.floor(math.sqrt((b[1]-a[1])^2+(b[2]-a[2])^2))*2
    for s=0,steps do
      local u=s/steps
      local x=a[1]+(b[1]-a[1])*u
      local yy=a[2]+(b[2]-a[2])*u
      local th=th0+(th1-th0)*(((i-1)+u)/(n-1))
      for dy=-th,th do for dx=-th,th do
        if dx*dx+dy*dy<=th*th then P(x+dx,yy+dy,col) end
      end end
    end
  end
end
local function foliage(cx,cy,r,n)
  for i=1,n do
    local a=rnd()*math.pi*2; local d=rnd()*r
    local ox,oy=cx+math.cos(a)*d,cy+math.sin(a)*d*0.8
    local s=2+rnd()*3
    for dy=-s,s do for dx=-s,s do
      if (dx*dx)/(s*s+0.2)+(dy*dy)/(s*s*0.7+0.2)<=1 then
        P(ox+dx,oy+dy,((dx-dy)/s>0.7) and C.FG or C.FG_D)
      end
    end end
  end
  for i=1,math.floor(n*0.8) do
    local a=-1.3+rnd()*1.7
    local x=cx+math.cos(a)*r*0.95
    local yy=cy+math.sin(a)*r*0.72
    if bay(x,yy)<0.55 then P(x,yy,C.RIM2) end
  end
end
twig({{-4,2},{26,12},{54,16},{80,28}},2.6,1.0,C.FG_D)
twig({{28,12},{38,0},{44,-8}},1.6,0.8,C.FG_D)
twig({{56,17},{70,4},{76,-6}},1.4,0.8,C.FG_D)
foliage(20,8,13,16) foliage(48,8,11,13) foliage(74,20,11,13) foliage(6,20,10,11)
twig({{484,6},{456,16},{432,14}},2.2,0.9,C.FG_D)
foliage(470,10,11,12) foliage(440,16,9,10)

spr:saveAs(spr.filename)
spr:saveAs(OUTP .. "Title_Dawn.png")
print("generated")
