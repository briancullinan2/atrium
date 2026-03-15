export function initLichtenberg(canvasId) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    let width, height;
    let isCancelled = false;

    const resize = () => {
        width = canvas.width = canvas.parentElement.clientWidth;
        height = canvas.height = canvas.parentElement.clientHeight;
    };
    window.addEventListener('resize', resize);
    resize();

    let time = 0;

    function project(x, y, z) {
        const factor = 500 / (z + 800);
        // Subtle floating shift
        const shiftX = Math.sin(time * 0.5) * 20;
        const shiftY = Math.cos(time * 0.5) * 15;
        return {
            // The | 0 trick is a very fast way to floor a float in JS
            x: ((x * factor) + (width / 2) + shiftX) | 0,
            y: ((y * factor) + (height / 2) + shiftY) | 0,
            scale: factor
        };
    }

    /**
     * Elemental Draw Logic
     * Types: 'stone', 'nature', 'electric', 'gold'
     */
    function drawElementalBranch(x1, y1, z1, x2, y2, z2, depth, type = 'electric', colors) {
        if (depth <= 0) return;

        const p1 = project(x1, y1, z1);
        const p2 = project(x2, y2, z2);
        const pulse = (Math.sin(time * 2 + depth) + 1) / 2;

        ctx.beginPath();
        ctx.moveTo(p1.x, p1.y);
        ctx.lineTo(p2.x, p2.y);

        // Styling based on SVG definitions
        switch (type) {
            case 'stone':
                const [br, bg, bb] = normalizeColor(colors.stone);
                ctx.strokeStyle = `rgba(${br}, ${bg}, ${bb}, ${0.3 + pulse * 0.2})`;
                ctx.lineWidth = depth * 1.5;
                ctx.shadowBlur = 0;
                break;
            case 'nature':
                ctx.strokeStyle = colors.nature; // natureBurst emerald
                ctx.lineWidth = depth * 0.8;
                ctx.shadowBlur = depth * 2;
                ctx.shadowColor = colors.nature;
                break;
            case 'electric':
                ctx.strokeStyle = colors.electric; // naturalGlow cyan
                ctx.lineWidth = depth * 0.4 * pulse;
                ctx.shadowBlur = depth * 5 * pulse;
                ctx.shadowColor = colors.electric;
                break;
            case 'gold':
                ctx.strokeStyle = colors.gold; // goldGrad
                ctx.lineWidth = 2;
                ctx.shadowBlur = 10;
                ctx.shadowColor = colors.gold;
                break;
        }

        ctx.stroke();

        // Recursion logic for 'electric' and 'nature' only
        if (type === 'electric' || type === 'nature') {
            const jitter = type === 'electric' ? 30 : 10;
            const midX = (x1 + x2) / 2 + (Math.random() - 0.5) * depth * jitter;
            const midY = (y1 + y2) / 2 + (Math.random() - 0.5) * depth * jitter;
            const midZ = (z1 + z2) / 2 + (Math.random() - 0.5) * depth * jitter;

            if (Math.random() > 0.2) {
                drawElementalBranch(x1, y1, z1, midX, midY, midZ, depth - 1, type, colors);
                drawElementalBranch(midX, midY, midZ, x2, y2, z2, depth - 1, type, colors);
            }
        }

        ctx.shadowBlur = 0;
        ctx.shadowColor = 'transparent';
    }

    function drawLocus(x, y, z, color) {
        const p = project(x, y, z);
        ctx.beginPath();
        ctx.arc(p.x, p.y, 4 * p.scale, 0, Math.PI * 2);
        ctx.fillStyle = color;
        ctx.shadowBlur = 15;
        ctx.shadowColor = color;
        ctx.fill();
        ctx.shadowBlur = 0;
        ctx.shadowColor = 'transparent';
    }


    let fps = 24;
    let fpsInterval = 1000 / fps;
    let lastDrawTime = performance.now();
    let colors = getThemeColors();
    let lastColorUpdate = performance.now();


    function animate(currentTime) {
        if (isCancelled) return;
        let elapsed = currentTime - lastDrawTime;
        if (elapsed < fpsInterval) {
            requestAnimationFrame(animate);
            return;
        }

        if (currentTime - lastColorUpdate > 1000) {
            colors = getThemeColors();
            lastColorUpdate = currentTime - (elapsed % fpsInterval);
        }

        lastDrawTime = currentTime - (elapsed % fpsInterval);


        time += 0.02;

        // Background trail (Secret Garden Ivory)
        const [br, bg, bb] = normalizeColor(colors.main);
        ctx.fillStyle = `rgba(${br}, ${bg}, ${bb}, 0.15)`;
        ctx.fillRect(0, 0, width, height);

        // 1. ANCHOR NODES (Elementals)
        const anchors = [
            { pos: [-300, 200, 0], col: colors.gold, type: 'gold' },
            { pos: [200, 200, 0], col: colors.gold, type: 'gold' },
            { pos: [0, -350, 100], col: colors.electric, type: 'electric' },
            { pos: [150, 0, -100], col: colors.nature, type: 'nature' }
        ];

        // 2. THE BIRDS (3 Orbitals)
        const birdCount = 3;
        for (let i = 0; i < birdCount; i++) {
            const angleOffset = i * (Math.PI * 2 / 3);
            const speed = time * (0.5 + i * 0.1);
            const r = 250 + Math.sin(time) * 50;

            const bx = Math.cos(speed + angleOffset) * r;
            const by = Math.sin(speed * 1.2) * 100;
            const bz = Math.sin(speed + angleOffset) * r;

            // Draw Bird Body
            drawElementalBranch(bx, by, bz, bx + 20, by - 10, bz, 2, 'gold', colors);

            // 3. LICHTENBERG STRIKES to Anchors
            anchors.forEach(a => {
                const dx = bx - a.pos[0];
                const dy = by - a.pos[1];
                const dz = bz - a.pos[2];
                const dist = Math.sqrt(dx * dx + dy * dy + dz * dz);

                if (dist < 400) {
                    drawElementalBranch(bx, by, bz, a.pos[0], a.pos[1], a.pos[2], 4, i === 1 ? 'nature' : 'electric', colors);
                }
            });
        }

        // 4. DRAW THE STRUCTURE (Stone Arch Base)
        const archNodes = [];
        for (let i = 0; i <= 10; i++) {
            const a = (i / 10) * Math.PI;
            archNodes.push([Math.cos(a) * 400, -Math.sin(a) * 400, 0]);
        }
        for (let i = 0; i < archNodes.length - 1; i++) {
            drawElementalBranch(...archNodes[i], ...archNodes[i + 1], 6, 'electric', colors);
        }

        // 5. DRAW NODES
        anchors.forEach(a => drawLocus(...a.pos, a.col));

        requestAnimationFrame(animate);
    }

    animate(performance.now());
    return {
        dispose: () => {
            isCancelled = true;
            cancelAnimationFrame(requestId);
            window.removeEventListener('resize', resize);
        }
    };

}