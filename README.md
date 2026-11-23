<div align="center">

# 🏔️ Dynamic 3D Procedural Terrain & Real-Time Mesh Deformation (Unity)

![Unity](https://img.shields.io/badge/Unity-2022.3%2B-black?style=for-the-badge&logo=unity)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![Input System](https://img.shields.io/badge/Unity-Input%20System-blue?style=for-the-badge)
![Optimization](https://img.shields.io/badge/Focus-Performance%20%26%20Jobs-success?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

[🌐 Version Française](#-français) • [🇬🇧 English Version](#-english)

</div>

---

## 🇫🇷 Français

### 📝 Présentation
Système complet de **génération de terrain 3D procédural par Chunks** développé sous Unity (C#). Ce projet met l'accent sur l'**optimisation bas niveau**, la **déformation dynamique de maillage en temps réel** et la **gestion multithreadée via le C# Job System**.

Il inclut un système complet de debug F1 permettant de monitorer les performances (allocation mémoire, nombre de sommets/triangles, FPS) et de tester plusieurs modes d'interaction et d'optimisation.

---

### ✨ Fonctionnalités Clés
- **📦 Système de Chunks Dynamique** : Allocation et extension automatique de la grille de terrain lors du déplacement aux bordures.
- **⚡ Optimisation & Multithreading** : Support optionnel du **C# Job System** (F8) pour recalculer les sommets et normales sans bloquer le thread principal.
- **📐 Systèmes de LOD (Level of Detail)** : Gestion dynamique du niveau de détail selon la distance de la caméra avec suivi direct de l'état des chunks (LOD 0, 1, 2 ou hors champ).
- **🎨 Dynamic Mesh Deformation** : 
  - Élever ou creuser le terrain en temps réel (Clic Gauche / Clic Droit).
  - Ajustement interactif des paramètres : intensité, rayon de voisinage, patterns de déformation et métriques de distance (Euclidienne, Manhattan, Chebyshev...).
  - Mode aperçu de déformation avant application.
- **🔄 Calcul des Normales à la Volée** : Plusieurs algorithmes de calcul des normales permutables en temps réel pour équilibrer précision visuelle et coût de calcul.
- **📊 Profiling & Debug Intégré** : Overlay d'informations (F1) affichant les métriques exactes :
  - Nombre de vertices / triangles (par chunk et total).
  - Empreinte mémoire exacte (Ko).
  - Taux de rafraîchissement (FPS).
  - Distribution des LODs et mode de calcul des normales.

---

### ⚙️ Commandes & Raccourcis UI

| Touche | Action |
| :--- | :--- |
| **F1** | Afficher / masquer la fenêtre de débogage |
| **F8** | Activer / désactiver le C# Job System (Multithreading) |
| **F10** | Basculer le mode de calcul des normales |
| **F2 / F3** | Changer de fonction mathématique / HeightMap |
| **F11 (Double clic)** | Réinitialiser le terrain |
| **Clic Gauche / Droit** | Élever / Creuser le terrain |
| **Espace (Maintenu)** | Aperçu de la zone de déformation |
| **Molette + Shift** | Ajuster l'intensité de déformation |
| **Molette + Ctrl** | Ajuster le rayon de voisinage |
| **Molette + Alt** | Proposer / Changer le pattern de déformation |
| **N (Maintenu)** | Visualiser les normales du maillage |
| **C** | Mettre en évidence les limites des chunks |

---

### 🛠️ Architecture Technique & Performances
- **Engine** : Unity 2022+ (Input System Package & TextMeshPro).
- **Mesh Generation** : Génération de maillages via code C#, manipulation directe des arrays de `Vector3` (`vertices`, `normals`, `uvs`) et indices de triangles.
- **Data Structures** : Grille de Chunks `Vector2Int` indexée dynamiquement avec libération propre de la mémoire.

---

---

## 🇬🇧 English

### 📝 Overview
A comprehensive **Chunk-based 3D Procedural Terrain Generation System** built with Unity (C#). This project highlights **low-level performance optimization**, **real-time dynamic mesh deformation**, and **multithreaded computing via the C# Job System**.

It features an integrated F1 debug overlay to monitor runtime metrics (memory footprint, vertex/triangle counts, real-time FPS) and to experiment with various optimization techniques.

---

### ✨ Key Features
- **📦 Dynamic Chunk System**: Automatic terrain grid expansion when navigating near border edges.
- **⚡ Multithreaded Optimization**: Optional **C# Job System** integration (toggleable via F8) to compute vertex heights and normals off the main thread.
- **📐 Dynamic LOD (Level of Detail)**: Cam-distance based detail scaling with real-time tracking of active chunks across LOD levels (LOD 0, 1, 2, culled).
- **🎨 Real-Time Mesh Deformation**:
  - Raise or dig terrain on-the-fly (Left Click / Right Click).
  - Interactive parameter adjustments: deformation intensity, influence radius, shape patterns, and distance metric calculations (Euclidean, Manhattan, Chebyshev, etc.).
  - Visual preview overlay before committing changes.
- **🔄 On-the-Fly Normal Recalculation**: Multiple normal calculation modes to balance visual fidelity vs computation cost.
- **📊 Built-in Profiling Overlay**: Detailed status panel (F1) rendering exact technical metrics:
  - Vertices and triangle count (per chunk & total).
  - RAM allocation estimate (KB).
  - Frame rate (FPS counter).
  - Real-time LOD state breakdown.

---

### ⚙️ Controls & Shortcuts

| Key | Action |
| :--- | :--- |
| **F1** | Toggle performance debug overlay |
| **F8** | Toggle C# Job System (Multithreading) |
| **F10** | Switch normal calculation algorithms |
| **F2 / F3** | Cycle math functions / HeightMaps |
| **F11 (Double click)** | Reset terrain mesh |
| **Left / Right Click** | Raise / Lower (Dig) terrain |
| **Space (Hold)** | Show deformation preview indicator |
| **Scroll + Shift** | Adjust deformation intensity |
| **Scroll + Ctrl** | Adjust deformation influence radius |
| **Scroll + Alt** | Cycle deformation patterns |
| **N (Hold)** | Visualize vertex normals |
| **C** | Highlight chunk borders |

---

### 🛠️ Technical Architecture
- **Engine**: Unity 2022+ (Input System Package & TextMeshPro).
- **Mesh Generation**: Custom procedural C# mesh builders directly manipulating `Vector3` arrays (`vertices`, `normals`, `uvs`) and index buffers.
- **Data Structures**: `Vector2Int` dynamic chunk grid mapping with efficient memory cleanup.

---

## 🚀 Getting Started / Installation

1. **Clone the repository**:
   ```bash
   git clone https://github.com/lucbr1/3D_procedural.git
   ```
2. Open the project folder using **Unity 2022.3 LTS** or newer.
3. Open the main scene located under `Assets/Scenes/`.
4. Press **Play** and press **F1** to view controls and performance stats.

---

### 👥 Authors
- **Lucas BIESZCZAD** ([@lucasbies](https://github.com/lucasbies))
- **Lucas BRUN** ([@lucbr1](https://github.com/lucbr1))

---
*Academic / Personal Project.*
