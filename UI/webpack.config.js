const path = require("path");
const MiniCssExtractPlugin = require("mini-css-extract-plugin");

// Mod output folder (the DLL's root folder, NOT a UI subfolder)
const appData = process.env.APPDATA || "";
const modOutputDir = path.join(
  appData,
  "../LocalLow/Colossal Order/Cities Skylines II/Mods/CityAgent"
);

module.exports = {
  entry: "./src/index.tsx",
  output: {
    path: modOutputDir,
    // Must match the DLL name — the game looks for {ModName}.mjs alongside {ModName}.dll
    filename: "CityAgent.mjs",
    // All relative asset URLs (images etc.) resolve under coui://ui-mods/
    // This is the shared host CS2 registers for all mod UI assets
    publicPath: "coui://ui-mods/",
    library: { type: "module" },
  },
  experiments: {
    outputModule: true,
  },
  resolve: {
    extensions: [".tsx", ".ts", ".js"],
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        use: "ts-loader",
        exclude: /node_modules/,
      },
      {
        test: /\.css$/,
        use: [MiniCssExtractPlugin.loader, "css-loader"],
      },
    ],
  },
  // CS2 exposes these as window globals at runtime — do NOT bundle them.
  // The game injects window.React, window["cs2/api"], etc. into the Coherent GT context.
  externalsType: "window",
  externals: {
    react: "React",
    "react-dom": "ReactDOM",
    "cs2/api": ["cs2/api"],
    "cs2/bindings": ["cs2/bindings"],
    "cs2/modding": ["cs2/modding"],
    "cs2/ui": ["cs2/ui"],
    "cs2/l10n": ["cs2/l10n"],
  },
  plugins: [
    new MiniCssExtractPlugin({
      // Must match the DLL name — the game auto-loads {ModName}.css when hasCSS=true
      filename: "CityAgent.css",
    }),
  ],
};
