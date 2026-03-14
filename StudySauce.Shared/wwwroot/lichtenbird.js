export function initLichtenberg(canvasId) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        console.error(`Canvas element with ID '${canvasId}' not found.`);
        return;
    }
    const ctx = canvas.getContext('2d');
    let width, height;

    const resize = () => {
        width = canvas.width = canvas.parentElement.clientWidth;
        height = canvas.height = canvas.parentElement.clientHeight;
    };
    window.addEventListener('resize', resize);
    resize();

    // Time variable for animation (not used for continuous rotation anymore)
    let time = 0;

    /**
     * 3D to 2D Projection
     * Modified from original to only apply very minimal, stabilized perspective.
     * Continuously oscillating on X and Y to create the 'shift' effect.
     */
    function project(x, y, z) {
        // Minimal fixed rotation (to show 3D form) rather than angle*0.5
        const fixedAngle = 0.4;
        const cos = Math.cos(fixedAngle);
        const sin = Math.sin(fixedAngle);

        // Standard perspective transformation logic
        let nx = x;
        let ny = y;
        let nz = z;

        // Apply slight fixed Y rotation (around vertical axis)
        const rx = nx * cos - nz * sin;
        const rz = nx * sin + nz * cos;
        nx = rx;
        nz = rz;

        // PERSPECTIVE
        const factor = 400 / (nz + 600); // 400 is zoom, 600 is depth offset

        // --- STABILIZATION & SHIFT ---
        // Create subtle back-and-forth shifts based on time and a sine wave.
        // Amplitude (how far) is 15px, Frequency (how fast) is time/5.
        const shiftX = Math.sin(time / 5) * 30;
        const shiftY = Math.cos(time / 5) * 20;

        return {
            // The | 0 trick is a very fast way to floor a float in JS
            x: ((nx * factor) + (width / 2) + shiftX) | 0,
            y: ((ny * factor) + (height / 2) + shiftY) | 0,
            nz: nz
        };
    }

    /**
     * Draws a Lichtenberg branch, modified to look like glowing veins in stone.
     */

    function drawBranch(x1, y1, z1, x2, y2, z2, depth, jitterScale = 10, isBaseStatue = true, colors) {
        if (depth <= 0) return;

        const p1 = project(x1, y1, z1);
        const p2 = project(x2, y2, z2);

        // Pulse logic based on time and depth (slightly slower pulse)
        const pulse = (Math.cos(time + (depth * 0.4)) + 1) / 2;

        // --- NEW STATUE & ENERGY COLORS ---
        let color, shadowColor, lineWidth;

        if (isBaseStatue) {
            // High depth: The core "Stone" structure
            if (depth > 4) {
                // Stone Gray with subtle pulse
                const [br, bg, bb] = normalizeColor(colors.stone);
                color = `rgb(${br * pulse}, ${bg * pulse}, ${bb * pulse})`;
                const [br2, bg2, bb2] = normalizeColor(colors.gold);
                shadowColor = `rgba(${br2}, ${bg2}, ${bb2}, 0.2)`;
                lineWidth = depth * 0.8;
            } else {
                // Low depth: Electric Cyan Veins growing on the stone
                const [br, bg, bb] = normalizeColor(colors.electric);
                color = `rgb(${pulse * br}, ${pulse * bg}, ${pulse * bb})`;
                const [br2, bg2, bb2] = normalizeColor(colors.nature);
                shadowColor = `rgba(${br2}, ${bg2}, ${bb2}, 0.4)`;
                lineWidth = depth * 0.6 * pulse;
            }
        } else {
            // We use this function for the Bird too (no jitter needed)
            color = colors.nature; // Light Gray/Silver Bird
            const [br, bg, bb] = normalizeColor(colors.main);
            shadowColor = `rgba(${br}, ${bg}, ${bb}, 0.4)`;
            lineWidth = 1.5;
        }

        ctx.beginPath();
        ctx.moveTo(p1.x, p1.y);
        ctx.lineTo(p2.x, p2.y);

        // Glowing effect
        ctx.shadowBlur = depth * 2.5;
        ctx.shadowColor = shadowColor;

        ctx.strokeStyle = color;
        ctx.lineWidth = lineWidth;
        ctx.stroke();

        ctx.shadowBlur = 0; // Performance Reset

        // --- RECURSION (Only for statue, not bird) ---
        if (isBaseStatue) {
            // Apply jitter Scale for recursive growth
            const midX = (x1 + x2) / 2 + (Math.random() - 0.5) * depth * jitterScale;
            const midY = (y1 + y2) / 2 + (Math.random() - 0.5) * depth * jitterScale;
            const midZ = (z1 + z2) / 2 + (Math.random() - 0.5) * depth * jitterScale;

            if (Math.random() > 0.15) { // 85% chance to bifurcate
                drawBranch(x1, y1, z1, midX, midY, midZ, depth - 1, jitterScale, isBaseStatue, colors);
                drawBranch(midX, midY, midZ, x2, y2, z2, depth - 1, jitterScale, isBaseStatue, colors);
            }
        }

        ctx.shadowBlur = 0;
        ctx.shadowColor = 'transparent';

    }


    function drawBirds(cubeNodes, colors) {
        const birdCount = 3;
        const orbitRadius = 280;
        const birdSize = 20;

        for (let i = 0; i < birdCount; i++) {
            // 1. UNIQUE ORBITALS
            // We add (i * (Math.PI * 2 / 3)) to space them 120 degrees apart
            // We vary the speed slightly per bird for more natural "growth"
            const offset = i * (Math.PI * 2 / birdCount);
            const speed = time * (0.4 + (i * 0.1));

            const birdCX = Math.cos(speed + offset) * orbitRadius;
            // Unique vertical bobbing per bird so they don't stay in the same plane
            const birdCY = Math.sin(speed * (1.2 + i * 0.2)) * (60 + i * 20);
            const birdCZ = Math.sin(speed + offset) * orbitRadius;

            // 2. DEFINE BIRD SHAPE (Head and Wings)
            // Orientation: the bird should "look" where it's going (tangent to circle)
            const dirX = -Math.sin(speed + offset);
            const dirZ = Math.cos(speed + offset);

            const head = [birdCX + dirX * birdSize, birdCY, birdCZ + dirZ * birdSize];
            const leftWing = [birdCX - dirZ * birdSize, birdCY + 10, birdCZ + dirX * birdSize];
            const rightWing = [birdCX + dirZ * birdSize, birdCY - 10, birdCZ - dirX * birdSize];

            // 3. DRAW BIRD BODY
            drawBranch(...head, ...leftWing, 2, 0, false, colors);
            drawBranch(...leftWing, ...rightWing, 2, 0, false, colors);
            drawBranch(...rightWing, ...head, 2, 0, false, colors);

            // 4. SHOCK THE STATUE (Proximity Strikes)
            cubeNodes.forEach((target, nodeIndex) => {
                const dx = birdCX - target[0];
                const dy = birdCY - target[1];
                const dz = birdCZ - target[2];
                const dist = Math.sqrt(dx * dx + dy * dy + dz * dz);

                // If this specific bird is close to a node, fire a branch
                if (dist < 350) {
                    // Alternating wings for the start point
                    const origin = (nodeIndex % 2 === 0) ? leftWing : rightWing;
                    drawBranch(origin[0], origin[1], origin[2], target[0], target[1], target[2], 4, 25, true, colors);
                }
            });
        }
    }


    let fps = 24;
    let fpsInterval = 1000 / fps;
    let lastDrawTime = performance.now();
    let isCancelled = false;
    let lastColorUpdate = performance.now();
    let colors = getThemeColors();

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

        // Clean slate with background (Warm Cream/Beige)
        const [br, bg, bb] = normalizeColor(colors.main);
        ctx.fillStyle = `rgba(${br}, ${bg}, ${bb}, 0.15)`;
        ctx.fillRect(0, 0, width, height);
        time += 0.03; // Increment animation timer

        // --- CUBE (The Statue) Setup ---
        const size = 300;
        const nodes = [
            [-size, -size, -size], [size, -size, -size],
            [size, size, -size], [-size, size, -size],
            [-size, -size, size], [size, -size, size],
            [size, size, size], [-size, size, size]
        ];


        // 1. Draw the Stabilized Cube Edges (Stone structure)
        for (let i = 0; i < 4; i++) {
            drawBranch(...nodes[i], ...nodes[(i + 1) % 4], 5, 0, true, colors); // Higher depth (5), zero jitter
            drawBranch(...nodes[i + 4], ...nodes[((i + 1) % 4) + 4], 5, 0, true, colors);
            drawBranch(...nodes[i], ...nodes[i + 4], 5, 0, true, colors);
        }

        // 2. Draw Lichtenberg Bursts from Center (0,0,0) to corners (The Cyan Veins)
        // This is where the recursive growth (isBaseStatue=true, jitter=20) really happens.
        //nodes.forEach(node => {
        //    drawBranch(0, 0, 0, node[0], node[1], node[2], 6, 20);
        //});

        // 3. Draw the Flying Triangular Bird
        drawBirds(nodes, colors);

        requestAnimationFrame(animate);
    }

    animate(0);

    return {
        dispose: () => {
            isCancelled = true;
            cancelAnimationFrame(requestId);
            window.removeEventListener('resize', resize);
        }
    };

}