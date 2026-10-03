# The Big Man Alliance - Browser Heist Front-End Prototype

Static front-end starting point for the browser-based heist game.

## Files

- `index.html` - main game-planning interface
- `styles.css` - responsive noir / heist visual design
- `app.js` - light prototype behavior for the heist slider, stat sliders, and crew selection
- `assets/big-man-alliance-logo.png` - provided team logo

## Running locally on Windows 11

The site has no build step. Open `index.html` directly in a browser, or serve the folder with any local HTTP server used by your development environment.

## Backend integration points

The current UI uses placeholder data. The main integration points are:

1. Character creator `Save Operative` and `Publish to Network` actions.
2. Crew network search and character cards.
3. Selected crew state currently stored only in `app.js` memory.
4. Heist launch payload, which should eventually combine selected character IDs, target value, and normalized difficulty.
5. Unity WebGL build mount point in the `#simulation` section.

The backend should remain authoritative for persisted character progression, equipment, XP, and validated heist results.
