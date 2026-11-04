(function () {
  const canvas = document.querySelector("canvas.scene");
  if (!canvas) return;
  const W = 320;
  const H = 64;
  const ground = 52;
  const ctx = canvas.getContext("2d");
  canvas.width = W;
  canvas.height = H;
  ctx.imageSmoothingEnabled = false;
  const S = {
    truckA: [0, 0, 22, 14], truckB: [23, 0, 22, 14], tower: [46, 0, 11, 28], pineL: [58, 0, 11, 13],
    pineS: [70, 0, 7, 9], drift: [78, 0, 17, 3], catA: [96, 0, 12, 11], catB: [109, 0, 12, 11],
    heart: [27, 29, 7, 6], noteA: [35, 29, 6, 7], noteB: [42, 29, 6, 7], moon: [49, 29, 9, 10],
    blink: [64, 29, 3, 3], flake: [68, 29, 3, 3],
  };
  let seed = 11;
  const rnd = () => {
    seed = (seed * 1103515245 + 12345) & 0x7fffffff;
    return seed / 0x7fffffff;
  };
  const towerX = W - 44;
  const catX = towerX - 24;
  const props = [];
  for (let x = 3; x < catX - 14; x += 9 + Math.floor(rnd() * 14)) props.push(["pineS", x, ground - 8 - Math.floor(rnd() * 2)]);
  for (let x = 10 + Math.floor(rnd() * 10); x < catX - 16; x += 26 + Math.floor(rnd() * 30)) props.push(["pineL", x, ground - 12]);
  props.push(["pineL", towerX + 22, ground - 12], ["pineS", towerX + 31, ground - 8]);
  for (let x = 6 + Math.floor(rnd() * 20); x < W - 10; x += 40 + Math.floor(rnd() * 40)) props.push(["drift", x, ground - 1]);
  const stars = [];
  while (stars.length < 16) {
    const x = 4 + Math.floor(rnd() * (W - 8));
    const y = 2 + Math.floor(rnd() * 16);
    if (x > 18 && x < 40 && y < 16) continue;
    stars.push([x, y]);
  }
  const flakes = Array.from({ length: 60 }, () => ({
    x: rnd() * W, y: rnd() * H, fall: 5 + rnd() * 6, phase: rnd() * 6.28, near: rnd() < 0.18,
  }));
  const notes = [];
  let truck = 40;
  let time = 0;
  let noteTimer = 0;
  const sprites = new Image();
  sprites.src = "/img/sprites.png";
  const still = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  function sprite(name, x, y) {
    const r = S[name];
    ctx.drawImage(sprites, r[0], r[1], r[2], r[3], Math.round(x), Math.round(y), r[2], r[3]);
  }

  function dot(x, y) {
    ctx.fillRect(Math.round(x), Math.round(y), 1, 1);
  }

  function line(x0, x1, y) {
    ctx.fillRect(x0, y, x1 - x0 + 1, 1);
  }

  function night() {
    const h = new Date().getHours();
    return h >= 18 || h < 7;
  }

  function update(dt) {
    time += dt;
    truck += 14 * dt;
    if (truck > W + 2) truck = -24;
    for (const f of flakes) {
      f.y += f.fall * dt;
      f.x += (Math.sin(time * 0.9 + f.phase) * (f.near ? 5 : 3) - 2) * dt;
      if (f.y >= H) {
        f.y = -2;
        f.x = rnd() * W;
      }
      if (f.x < -3) f.x += W + 6;
    }
    noteTimer -= dt;
    if (noteTimer <= 0) {
      notes.push({ x: towerX + 6, y: ground - 34, age: 0, kind: rnd() < 0.5 ? "noteA" : "noteB" });
      noteTimer = 0.9 + rnd() * 0.8;
    }
    for (const n of notes) {
      n.age += dt;
      n.y -= 8 * dt;
      n.x += (Math.sin(n.age * 2.4) * 4 - 3) * dt;
    }
    while (notes.length && notes[0].age > 2.6) notes.shift();
  }

  function draw() {
    ctx.fillStyle = "#000";
    ctx.fillRect(0, 0, W, H);
    ctx.fillStyle = "#fff";
    const dark = night();
    if (dark) {
      stars.forEach((s, i) => {
        if (Math.sin(time * 1.7 + i * 2.3) > -0.6) dot(s[0], s[1]);
      });
      sprite("moon", 22, 3);
    }
    for (const p of props) sprite(p[0], p[1], p[2]);
    line(0, W, ground + 1);
    for (let row = 0; row < 3; row++)
      for (let x = row * 3; x < W; x += 5 + row * 2) dot(x, ground + 3 + row * 2);
    sprite("tower", towerX + 2, ground - 26);
    if (Math.floor(time * 2) % 2 === 0) sprite("blink", towerX + 6, ground - 28);
    const bob = Math.floor(time * 1.6) % 2 === 0;
    sprite(bob ? "catA" : "catB", catX + 2, ground - 9);
    const x = Math.round(truck);
    const y = ground - 12 + (Math.floor(truck / 9) % 5 === 0 ? -1 : 0);
    if (dark) {
      line(x + 21, x + 23, y + 7);
      for (let i = 4; i <= 24; i += 2) dot(x + 21 + i, y + 7 + Math.floor((ground - y - 7) * i / 24));
      line(x + 24, x + 55, ground + 2);
      line(x + 28, x + 47, ground + 3);
      line(x + 33, x + 40, ground + 4);
    }
    sprite(Math.floor(truck / 4) % 2 === 0 ? "truckB" : "truckA", x, y);
    for (const n of notes) {
      if (n.age > 2.2 && Math.floor(n.age * 10) % 2 === 0) continue;
      sprite(n.kind, n.x, n.y);
    }
    for (const f of flakes) {
      if (f.near) sprite("flake", f.x - 1, f.y - 1);
      else dot(f.x, f.y);
    }
  }

  let last = 0;
  function frame(now) {
    const dt = Math.min(0.1, (now - last) / 1000 || 0);
    last = now;
    update(dt);
    draw();
    requestAnimationFrame(frame);
  }

  sprites.onload = () => {
    if (still) {
      update(3);
      draw();
      return;
    }
    requestAnimationFrame((t) => {
      last = t;
      requestAnimationFrame(frame);
    });
  };
})();
