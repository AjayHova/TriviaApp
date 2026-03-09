(function () {
    const canvas = document.createElement('canvas');
    canvas.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;pointer-events:none;z-index:0;';
    document.body.appendChild(canvas);

    const ctx = canvas.getContext('2d');
    let flakes = [];

    function resize() {
        canvas.width  = window.innerWidth;
        canvas.height = window.innerHeight;
    }
    window.addEventListener('resize', resize);
    resize();

    const SYMBOLS = ['❄', '❅', '❆', '*', '·', '•'];

    function rand(min, max) { return Math.random() * (max - min) + min; }

    function createFlake() {
        return {
            x:       rand(0, canvas.width),
            y:       rand(-50, -10),
            size:    rand(8, 22),
            speed:   rand(0.6, 2.8),
            drift:   rand(-0.4, 0.4),
            opacity: rand(0.15, 0.7),
            symbol:  SYMBOLS[Math.floor(Math.random() * SYMBOLS.length)],
            wobble:      rand(0, Math.PI * 2),
            wobbleSpeed: rand(0.005, 0.025),
            wobbleAmp:   rand(0.3, 1.2),
        };
    }

    // Seed initial flakes spread across the full screen height
    for (let i = 0; i < 80; i++) {
        const f = createFlake();
        f.y = rand(0, canvas.height);
        flakes.push(f);
    }

    function animate() {
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        for (let i = flakes.length - 1; i >= 0; i--) {
            const f = flakes[i];

            f.wobble += f.wobbleSpeed;
            f.x += f.drift + Math.sin(f.wobble) * f.wobbleAmp;
            f.y += f.speed;

            ctx.globalAlpha = f.opacity;
            ctx.font = `${f.size}px serif`;
            ctx.fillStyle = '#ffffff';
            ctx.fillText(f.symbol, f.x, f.y);

            // Remove if off screen, replace with new flake at top
            if (f.y > canvas.height + 30 || f.x < -30 || f.x > canvas.width + 30) {
                flakes.splice(i, 1);
                flakes.push(createFlake());
            }
        }

        ctx.globalAlpha = 1;
        requestAnimationFrame(animate);
    }

    animate();
})();
