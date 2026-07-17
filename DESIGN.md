# Roofing Co-op — Documento de Diseño (visión consolidada)

> Juego de techado cooperativo en **primera persona con cuerpo**, físico-cómico estilo **PEAK**,
> con progresión y tienda estilo **REPO**. El placer central: el peligro de caer (te salvas con
> estamina/cuerda), techar bien por pasos realistas para ganar dinero, pelear contra el clima
> dinámico, y reírte/trollear con amigos. **No hay muerte**: la vida es la estamina y siempre
> queda una reserva.

Última actualización: 2026-06-12. Este documento manda sobre el `spec.md` original (que describía
el modelo "putty/masilla", ahora reemplazado por el techado por pasos).

---

## 1. Pilares (decididos con el cuestionario)

| Pilar | Decisión |
|------|----------|
| Perspectiva | Primera persona **siempre**, con cuerpo visible |
| Tono | Físico-cómico torpe (PEAK), satisfacción + caos cooperativo |
| Movimiento | Balance físico/arcade; sprint con estamina; salto, agacharse |
| Caer | Lastima y reduce la **capacidad** de estamina poco a poco (PEAK). Sin muerte |
| Salvarte | Agarre de borde si te queda estamina; arnés-cuerda opcional con físicas reales |
| Colisión | Los jugadores **no se atraviesan** (empujarse = troleo deseado) |
| Subir | Escalera (W), saltar entre techos, cuerdas largas. La escalera puede caerse si está mal puesta |
| Núcleo techado | **Pasos por celda**: arrancar → reparar madera → fieltro → clavar tejas. Tejas = objetos físicos. Calidad/material importan |
| Orden | Flexible, pero el orden afecta la satisfacción del cliente → recomendación/popularidad |
| Escombros | Hay que recogerlos y tirarlos |
| Inventario | 2 manos; cinturón para herramientas; zona de equipamiento/material al inicio |
| Clima | Despejado base; lluvia/viento/nieve/rayo/calor/frío pueden aparecer y **cambiar durante** el nivel. Neblina sí |
| Temperatura | Frío → necesitas calor; calor → necesitas refrescarte |
| Niveles | **Procedurales**: varían tamaño, inclinación, complejidad, clima, altura. Casi aleatorio, con probabilidad creciente de cosas peores |
| Economía | Ganas dinero; tienda entre niveles. Compras **ayudan, no automatizan**. Consumibles + permanentes. Herramientas se compran y mejoran |
| Multijugador | Idea central (2-4) pero jugable solo. Repartir tareas, **rescate** de compañeros, fricción física |
| Fallo | Reintento con multa de dinero |
| Persistencia | Se guarda entre sesiones |
| Arte | Low-poly liviano, assets gratis, exigencia gráfica baja (target: Steam, PC modesta) |
| Audio | Para el final |

---

## 2. Estado actual del código (qué ya existe y sirve)

- **Cámara FP** (`Input/CameraController.cs`) y **raycast de herramienta** (`Input/PlayerInput.cs`) — reutilizados.
- **Career/Jobs** (`Core/*`, `Gameplay/JobConfiguration*`, `Gameplay/JobCatalog`), **guardado** (`Persistence/SaveManager`),
  **multijugador Mirror** (`Multiplayer/*`) — intactos, listos para reconectar.
- Modelo "putty/masilla" (`Gameplay/RoofSurface`, `RoofingMaterial*`, `MaterialPhysics`) — **en pausa**,
  reemplazado por el techado por celdas. No se borró; se puede canibalizar.

---

## 3. Lo construido en esta pasada (Fase A jugable + techado v1)

**Jugador (`Assets/Scripts/Player/`)**
- `PlayerStamina` — estamina = vida; daño de caída reduce la capacidad (zona "lesionada" que sana lento); reserva mínima (nunca te bloquea del todo).
- `PlayerLocomotion` — CharacterController: WASD, sprint (gasta estamina), salto, agacharse, gravedad, **pendiente** (frena + desliza en empinado), head-bob, viento/empujones, penalización por cargar.
- `SafetyHarness` — cuerda física opcional (tecla **F**): te ancla a un punto del techo/cumbrera, te limita a su longitud y **frena la caída** al tensarse. Longitud y fiabilidad mejorables (tienda). Dibuja la cuerda.
- `LedgeGrabAndFall` — al caer, si te queda estamina **te agarras de un borde** automáticamente y quedas colgado; **W/Espacio** para subir (gasta estamina), **Ctrl/S** para soltarte. Caídas grandes aturden (placeholder de ragdoll).
- `LadderClimber` — entra a la zona de la escalera y empuja **W** para trepar; arriba pisas el techo; **Espacio** para saltar de la escalera.
- `PlayerRigBuilder` — ensambla todo el jugador FP por código (cuerpo + cámara + herramientas).

**Mundo (`Assets/Scripts/World/`)**
- `HouseBuilder` — genera casa: suelo, paredes, **techo a dos aguas inclinado** (pitch configurable, 8/12 por defecto), topes de hastial, **ancla de cumbrera** para el arnés, y **escalera** apoyada.
- `Ladder` — escalera con zona de trepado y puntos de subida/bajada (con opción de volverse inestable/caerse).

**Techado por celdas (`Assets/Scripts/Gameplay/`)**
- `RoofingProcess` — enums de herramientas y estados + `RoofCell` (cada celda avanza con la herramienta correcta y se recolorea).
- `RoofGrid` — divide el techo en celdas y conecta el click del jugador para techar; avisa cuando el techo está completo.
- `RoofingToolbelt` — selección de herramienta (**1-4 / scroll**), mira (crosshair) y lectura de progreso en pantalla.

**Integración**
- `Core/JobSceneController` — reescrito: arma el mundo + jugador + grilla al darle Play; cierra el loop al completar el techo. Funciona **standalone** (sin career) como playtest.

---

## 4. Cómo probar (playtest)

1. En Unity, abre `Assets/Scenes/RoofingJob.unity`.
2. Asegúrate de que haya **un** GameObject con el componente `JobSceneController` (puede estar vacío; él construye todo). Si la escena trae una cámara cenital vieja, el controlador la desactiva sola.
3. Pulsa **Play** → aparece el **TABLERO DE CONTRATOS** (Fase D): toma una oferta con **1/2/3**
   (**R** = otras ofertas) y el mundo se construye según el contrato elegido. **[T]** abre la
   **TIENDA** (Fase E): equipo permanente ([1-5]) y **packs de consumibles** ([6-8]) — solo
   con dinero en mano. El equipo se aplica en cada contrato; los consumibles se gastan en la
   obra con **[Q] usar / [X] cambiar** (chip abajo a la izquierda).

**Controles:**
- **WASD** moverse · **ratón** mirar · **Shift** correr · **Espacio** saltar · **Ctrl/C** agacharse
- **F** poner/quitar arnés (mira hacia el techo/cumbrera y pulsa F)
- **1-4 / scroll** elegir ranura del cinturón · **5** manos (siempre disponible) · **R** (con martillo) cambia el clavo · **Click izq.** usar herramienta sobre la teja mirada (clavar centrado = más calidad) · **Click der. (mantener, con clavadora)** zoom de precisión con lectura de puntaje en vivo
- **E** interactuar: recoger escombros, recargar material en el pallet, tirar escombros en el contenedor, **tomar una herramienta del rack del suelo** (entra en tu ranura activa)
- **G** suelta lo que cargas; con un **rollo de fieltro** mirando al techo lo **despliega** en esa hilada → **E** lo agarra y se arrastra **horizontalmente a lo largo de la hilada** para desenrollarlo (E de nuevo = soltar; si ya no puede desenrollar, E = recoger). Coger un material distinto con las manos ocupadas **suelta lo anterior al suelo** (nunca se pierde)
- Caer cerca de un borde + tener estamina = te agarras solo; **W** para subir
- **Enter** entregar el trabajo cuando el techo esté completo (muestra calidad/limpieza/pago;
  el dinero se **acredita al career y se guarda** — abajo a la derecha ves "Dinero: $X", que
  **persiste entre sesiones**; en playtest directo usa el career "Playtest"). Tras entregar,
  **Backspace** vuelve al **tablero** con ofertas nuevas (más bravas: acabas de subir de nivel)
- **Backspace** con un contrato tomado = **abandonar** → pide confirmación y cobra **multa**
  (25% del pago estimado, **se cobra aunque quedes en deuda** — el chip pasa a "DEUDA" en rojo):
  **[R]** paga y **reintenta la misma casa** desde cero (clima nuevo, mismo pronóstico) ·
  **[Backspace]** paga y vuelve al tablero · **[Enter]** cancela y sigues trabajando
- (En el editor de Unity, **Esc** libera el ratón de la ventana de juego.)

**Obstáculos (Etapa D3):** si el contrato trae **chimenea** o **claraboya**, esas celdas no se
techan — el marco verde y la diagonal **rodean** el obstáculo. **No te pares en la claraboya**:
cruje y al segundo se **raja** (lesión + empujón pendiente abajo). El **árbol** junto a la
escalera solo estorba (el tronco es sólido, la copa no).

**Techo completo (Etapa D4):** los contratos de **"2 aguas"** incluyen la cara trasera —
**cruza por la cumbrera** (con arnés mejor) para trabajarla; el HUD suma todas las caras y
avisa "¡Agua terminada!" cuando acabas una. Las **casas en L** añaden un ala baja
perpendicular con **2 caras más** y su **escalera pequeña** (también puedes saltar del techo
principal al del ala). Cada cara tiene su propio orden diagonal de tejas.

**Techos a 4 aguas (Etapa D5):** sin hastiales — las puntas también son **caras inclinadas**
(triángulos) y las principales son trapecios. Las celdas cortadas por el **limatón** no
existen (el marco verde lo rodea) y el borde queda tapado por las **tapas de limatón**. Para
pasar de una cara a otra **camina por encima del limatón** (las tapas no estorban). La
cumbrera es corta: el rayo y los cuervos lo saben.

**Ciclo de material/escombros:** arrancar tejas viejas genera **escombros físicos** que caen; recógelos (**E**, ocupan tus manos y te frenan) y tíralos en el **contenedor** — o usa la **carretilla**: aparca la carretilla donde caen (los traga sola, hasta 30), o échale lo que llevas con **E**; empújala (**E** con manos libres) hasta el contenedor y vacíala **de a 6** con **E**. Limpieza y calidad afectan el **pago** final. El **pallet** recarga madera/fieltro/tejas — **dentro del presupuesto del contrato es gratis** (se anuncia al empezar); pasarte saca un chip rojo "Material de más" y **se descuenta del pago** (Etapa E3). Recoger lo tirado y reutilizar retazos no cuesta.

**Clima:** cambia solo durante el nivel (HUD arriba a la derecha; avisa 12 s antes de cada
cambio). La **lluvia** empapa el techo y **resbala antes y más fuerte** (las espumas siguen
agarrando); el **viento** empuja — más en lo alto — y en tormenta trae **ráfagas**: ponte el
arnés (F). En **niveles fríos** nieva en vez de llover: la nieve **tapa las celdas** (no se
pueden trabajar) y se **limpia con la pala** (1-2 paladas); la ventisca además corta la
visibilidad. La nieve resbala, y al derretirse moja. La **neblina** deja el techo casi a
ciegas. En **tormenta**, quedarte de pie en la cumbrera te convierte en **pararrayos**: sale
un banner rojo con barra de carga — **agáchate (Ctrl) o baja**; si te alcanza, te tira y
quema estamina máxima (sin muerte). **Temperatura**: en día frío la estamina regenera lento
(muévete o baja a calentarte); en ola de calor todo cansa más al sol (descansa a la sombra).

**Flujo esperado:** apareces en el suelo → caminas a la escalera → **W** para subir → pisas el techo
inclinado (sientes el tirón de la pendiente; pon el arnés con F) → empiezas con 4 herramientas en el cinturón
(**pala, palanca, martillo, rodillo**). Con la **pala** arrancas; si la madera está dañada, la **palanca** saca
la tabla vieja, con la **mano** (tecla 5, llevando una plancha) **colocas la plancha nueva** sobre el hueco y el
**martillo la clava** (la sana se salta el paso); el **rodillo** pone fieltro. Luego
vuelves al **rack** del suelo (**E**) e intercambias por **mano** (poner teja) y **clavadora** (clavar) →
con la mano fuera, **sigue el marco VERDE**: se empieza en una **esquina del alero** y se sube **en
diagonal escalonada** (fuera de orden = -25% calidad) → cada celda cambia de color → al terminar todas,
"ROOF COMPLETE" → **Enter**.

---

## 5. Roadmap por fases (apilando sobre esta base)

- **Fase A — Fundación FP jugable** ✅ *(esta pasada)*: movimiento, casa, escalera, pendiente, arnés, agarre, techado v1.
- **Fase B — Techado completo** *(ola 1 hecha)*: ✅ tejas rectangulares con capas físicas por etapa, ✅ calidad/precisión, ✅ gasto de material + recarga, ✅ escombros a recoger/tirar, ✅ pago por calidad/limpieza, ✅ **dinero/calidad reconectados a `Career`/`SaveManager`** *(2026-07-13: al entregar, el pago se acredita al career con historial —calidad, limpieza, tiempo, rating— se **guarda en disco** y desbloquea el siguiente trabajo; HUD muestra "Dinero: $X"; el playtest standalone usa un career "Playtest" que también persiste entre Plays)*. ⏳ Pendiente: tejas individuales como rigidbodies, satisfacción del cliente según orden. (`RoofingJobInstance` queda en pausa con el flujo putty; la conexión nueva es directa.)
- **Fase C — Clima dinámico** ✅ *(completa — desglose en §8)*: lluvia (resbala) ✅, viento (empuja) ✅, **ventisca/nieve** (tapa el techo, hay que limpiar) ✅, neblina (visibilidad) ✅, rayo (peligro en lo alto) ✅, temperatura (frío/calor) ✅. Cambios por etapas dentro del nivel ✅.
- **Fase D — Niveles procedurales (REPO)** ✅ *(completa — desglose en §9)*: generador que aleatoriza `HouseSpec` + clima con probabilidad creciente de dificultad ✅; selección de nivel ✅; multas por fallo + reintento (con **deuda** permitida) ✅; obstáculos estructurales (chimenea/claraboya/árbol) ✅; multi-cara: techos a 2 aguas + **casas en L** ✅; **techos a 4 aguas** (trapecios + triángulos, tapas de limatón) ✅.
- **Fase E — Economía y tienda** ✅ *(completa — desglose en §10)*: dinero por trabajo ✅ (Fase B/D); **tienda en el tablero ([T]) con equipo permanente persistente** ✅ (cantimplora, ropa térmica, botas anti-resbalón, arnés reforzado, clavadora calibrada — ayudan sin automatizar, sin fiado); **consumibles por packs** ✅ (sal de deshielo, termo de café, cuerda de emergencia — [Q] usar / [X] cambiar en la obra); **presupuesto de material por contrato** ✅ (recargar dentro es gratis, el EXTRA se descuenta del pago; reutilizar retazos no cuesta).
- **Fase F — Multijugador**: sincronizar con Mirror el cuerpo, escalada, arnés, clima, **rescate** de caídos y la fricción física entre jugadores.
- **Fase G — Arte y audio**: reemplazar primitivas por assets low-poly gratis (Synty/Kenney/Mixamo), ragdoll real, SFX (viento, clavos, crujidos, gritos).

---

## 6. Notas técnicas / TODO conocidos

- El **cuerpo y herramientas son primitivas placeholder**; reemplazables por assets gratis sin tocar la lógica.
- El **ragdoll** es un aturdimiento simplificado (no hay rig articulado todavía) → Fase G.
- El **enredo de cuerda** está simplificado (sólo limita por longitud) → mejorar en Fase B/E.
- El **empuje entre jugadores** real (no atravesarse + troleo) llega con la red en Fase F; ahora el CharacterController ya da colisión sólida.
- ✅ Reconectado `RoofGrid` → career (dinero, calidad, limpieza, guardado) directo desde
  `JobSceneController.FinishAndPay` → `JobCompletion` → `CareerManager`. `RoofingJobInstance`
  sigue en pausa con el flujo putty (el multijugador viejo aún lo usa; se migra en Fase F).

---

## 7. Realismo v2 — Roadmap detallado (pasada actual, basado en fotos reales del oficio)

> Origen: Miguel vio el trabajo en persona y trajo fotos + una lista de mejoras de realismo.
> Aquí se separan en **etapas ordenadas por dependencia** (cada una se apoya en la anterior).
> Regla: hacemos una etapa, compilas y mandas captura, ajustamos, y seguimos.

### Datos reales de referencia (medidas del oficio)
- **Escala:** 1 unidad de Unity = **1 metro**.
- **Persona:** ~**1.75 m** de alto (ojos ~1.62 m).
- **Bulto de tejas:** ~**1.0 m × 0.32 m** (alto del paquete ~0.10 m). 3 bultos ≈ 1 "square" (9.3 m²).
- **Lámina de OSB/plywood:** **1.22 × 2.44 m** (4×8 pies), grosor ~1.2 cm.
- **Vigas (rafters):** separadas ~**40 cm** (16") entre centros; debajo del deck hay **hueco + aislamiento** visible.
- **Teja (shingle):** **1.0 × 0.32 m** cada tira (medida real). Exposición visible ~14 cm por hilada al
  solaparse. El **bulto** es una pila de tejas con el mismo footprint 1.0 × 0.32 m.
- **Casa:** huella 8×10 m, **1 o 2 pisos** (2.6 m por piso; por defecto **2 pisos** = eave 5.2 m, caída
  más temible), pendiente **7/12 (30°)**.
- **Celdas vs tejas:** la grilla de **10×6 celdas** son *unidades de trabajo*, NO tejas sueltas (sería
  ~280 clics). Dentro de cada celda se dibujan tejas reales de 1×0.32 (Etapa 4) para verse realista.
- **Colocación de tejas:** starter en el alero → arrancar desde una **esquina inferior** → hiladas con
  **desfase** subiendo en **diagonal escalonada** (método stair-step).
- **Fieltro:** se extiende rodando el rollo y se fija con **clavos de tapa plástica** (cap nails).

### Etapas

**Etapa 0 — Escala real (BASE de todo lo demás)** ✅ *(hecha)*
Mundo a metros reales: jugador 1.78 m / ojos 1.65 m; casa 8×10 m de **2 pisos** (eave 5.2 m);
pendiente **7/12 (30°)** con resbalón a 28° (mantiene el "tirón"); celda de teja = exactamente **1.0 m**
de ancho; **bulto real 1.0 × 0.32 m** (al soltar y en el pallet). *Tocó:* `HouseBuilder`,
`PlayerRigBuilder`, `PlayerLocomotion`, `JobSceneController`, `DroppedMaterial`.

**Etapa 1 — Estructura real del techo (el "hueco" bajo la madera)** ✅ *(hecha)*
Flujo de madera dañada (bloque 2×3): **arrancar → palanca (saca clavos + levanta la tabla vieja) →
hueco a la vista (vigas + aislamiento amarillo) → **medir el hueco (metro)** → **cortar la plancha
(sierra)** si no es de lámina entera → colocar plancha (mano) → clavarla (martillo) →
fieltro → tejas**. La
madera **sana** va directo a fieltro (no se toca). Nueva herramienta **Palanca** (`PryBar`, tecla **2**)
con modelo en mano + animación de palanqueo; nuevo estado `DeckRemoved` con visual de vigas+aislamiento.
Casa de **1 ó 2 pisos aleatorio** por nivel. *Tocó:* `RoofingProcess`, `RoofGrid`, `RoofingToolbelt`,
`HeldToolView`, `JobSceneController`.
⏳ *Falta puliendo (más adelante):* madera por láminas 4×8 reales (hoy es por bloque 2×3) y textura/tamaño
de madera variable (eso entra con la cortadora en la Etapa 6).

**Etapa 2 — Inventario funcional (4 herramientas) + más herramientas** ✅ *(hecha)*
Barra de **4 ranuras** (teclas 1-4 / scroll): solo cargas 4 a la vez; el resto están **en el suelo**
en un **rack/lona** junto al material. Miras una y con **E** la cambias por la de tu ranura activa
(la que tenías vuelve al rack). Se agregaron **6 herramientas nuevas** con modelo en mano + en el
suelo (con etiqueta): **taladro, cortadora, metro, cuchilla, tiza, clavadora de tapa**. *Loadout
inicial:* pala, palanca, martillo, rodillo (mano y clavadora se traen del rack). *Tocó:* `RoofingTool`
enum, `RoofingToolbelt` (inventario), nuevos `World/ToolRack` + `ToolPickup` + `ToolVisuals` +
`Billboard`, `HeldToolView` (6 modelos), `JobSceneController` (rack).
✅ *Funciones reales (pasada 2b/2c):* **taladro** destornilla los **4 tornillos de las esquinas** de la
antena (ahora más grande) → se suelta y cae como **escombro** a botar; **ventilación y parabólica** son
**fijas** (se quedan, se techa alrededor); **cortadora** marca el corte de la madera según la división de
tejas (kerf; el corte a medida completo llega en E6); **tiza** marca **línea guía entre 2 puntos** (igual
que el metro; hiladas marcadas = +calidad); el **martillo** tiene **menú de clavos** (tecla **R**): *clavo de
techo* / *clavo de tapa* (este último **fija el fieltro**, +calidad); **cuchilla** recorta el fieltro
(+calidad); **metro** mide la distancia entre 2 puntos. La **mano** está **siempre disponible (tecla 5)**,
fuera del rack. *Quitada* la "clavadora de tapa" como herramienta. Rack reubicado fuera de la casa; los
nombres salen **solo al apuntar** (prompt de E), no flotando en el cielo. Los **tornillos** llevan textura
metálica + cruz, **animación de salida** (suben girando) y desaparecen; la **antena cae como antena**
(no como bloque) y se recoge/bota. *Toca:* `World/RoofFixture` +
`FixtureScrew`, `Gameplay/ToolFunctions`, `RoofGrid`, `RoofingToolbelt`, `JobSceneController`.

**Etapa 3 — Fieltro interactivo (rodar + fijar)** ✅ *(hecha)*
Al pasar el **rodillo**, un **rollo de fieltro visible** sigue el borde de avance y se va **extendiendo**
hilada arriba (se ve cómo se desenrolla); el fieltro se **fija** con el **martillo + clavo de tapa**
(tecla **R**, +calidad). *Tocó:* `RoofGrid` (rollo visible que rueda).
✅ *Versión completa (rollo físico, HORIZONTAL):* con un **rollo de fieltro en las manos**, mira al techo y
pulsa **G** para **desplegarlo** en esa hilada (se abre cubriendo su celda). Míralo y pulsa **E** para
**agarrarlo**; arrástralo **a lo largo de la hilada (horizontal)** mirando hacia donde quieres llevarlo —
se desenrolla tendiendo fieltro real celda a celda (gasta sus propias unidades, no las manos), **gira** al
rodar y **adelgaza** al gastarse. El **eje del rollo apunta pendiente arriba**, así la gravedad no puede
llevárselo rodando cuesta abajo (feedback de Miguel: desenrollar en vertical no tenía sentido). No puede
pasar por celdas sin preparar (tejas viejas / hueco abierto). **E** de nuevo lo suelta donde está; si ya no
puede desenrollar (bloqueado o hilada lista), **E** lo recoge a las manos. Al acabarse el fieltro, el
**tubo de cartón** cae como **escombro** para botar. El rodillo sigue como método rápido alternativo y
también extiende **a lo largo de la hilada**. *Tocó:* nuevo `Gameplay/DeployableFeltRoll`, `RoofGrid`
(despliegue con G + API del rollo + limpieza del rollo fantasma por hilada), `PlayerMaterials` (hook de
soltado), `PlayerInteractor` (cámara/tecla expuestas), `RoofingToolbelt` (ayuda).

**Pulido post-feedback (captura del 2026-07-10)** ✅
- **Tornillos de la antena:** ahora **grises** (acero), cabeza **baja** de tornillo real, y **tornillo
  completo** (cabeza + vástago que se ve salir al destornillar); al soltarse sale **volando entero** con
  físicas y desaparece a los segundos. (`RoofFixture`)
- **Clavos de tapa / recorte de cuchilla:** viven en una capa sobre el fieltro que se **oculta cuando la
  teja los cubre** — ya no atraviesan la textura. (`RoofCell.FeltDecor`, `RoofGrid`)
- **Tejas con vida:** al colocarlas **caen con una ligera inclinación y se asientan con rebote**; al
  clavarlas se **prensan** un instante. Ya no aparecen rígidas. (`RoofCell`)
- **Inventario que nunca borra:** coger un material/escombro distinto con las manos ocupadas **suelta lo
  anterior al suelo como paquete recogible** (intercambio), en vez de perderse. (`PlayerMaterials`)
- **Plywood en dos pasos:** nueva etapa `DeckPlaced` — la plancha se **coloca a mano** (queda pálida y
  levantada) y luego se **clava** con el martillo (queda al ras). (`RoofingProcess`, `RoofGrid`)

**Etapa 4 — Colocación de tejas correcta (diagonal escalonada)** ✅ *(hecha)*
- **Orden escalonado real:** la hilada del **alero** se empieza por una **esquina inferior** y crece de
  lado; cada hilada superior necesita la celda de **abajo ya tejada** (eso construye la diagonal) y crece
  desde sus propias tejas. Con la **mano (5)** en uso, la grilla **resalta en VERDE** las celdas válidas
  siguientes; colocar **fuera de orden se permite pero cuesta -25% de calidad** al clavar (HUD lo avisa).
- **Starter en el alero:** banda oscura que asoma bajo la primera hilada en cuanto está el fieltro.
- **Tejas reales dentro de la celda:** cada celda se dibuja como **mini-hiladas de ~14 cm de exposición**
  (medida real), cada una con su tono, borde de tope más oscuro y **junta alternada a mitad de teja**
  (el desfase clásico). Ya no es una plancha lisa.
- **Tiza corregida a hiladas:** la línea de tiza ahora marca las **hiladas (cursos)** que cruza — como
  las guías reales de obra — y esas hiladas puntúan más al clavar.
- **Apilación de bultos en el techo:** lo que sueltas (G) **sobre el techo** queda apoyado plano y **no se
  desliza** (kinemático); puedes subir y apilar bultos como en la obra.
*Tocó:* `RoofingProcess` (mini-hiladas, starter, resalte), `RoofGrid` (regla escalonada + penalización +
resalte + tiza por hiladas), `RoofingToolbelt` (pista), `DroppedMaterial` (bultos fijos en el techo).

**Etapa 5 — Carretilla de escombros** ✅ *(hecha)*
Carretilla **física** (Rigidbody, aparece entre el spawn y el contenedor):
- Los escombros que **caen dentro de la bandeja se acumulan solos** (hasta **30**) — aparcarla bajo el
  alero mientras arrancas es la jugada. La pila **se ve crecer** por capas y la carretilla **pesa más**
  cuanto más llena.
- **E con escombros en las manos** = echarlos a la carretilla (sin crédito de limpieza aún).
- **E con manos libres** = **empujarla**: te sigue por delante con físicas reales, la **rueda gira**,
  choca con el mundo. **E** de nuevo la suelta; alejarse también.
- Junto al **contenedor**, **E** la **vacía de a 6** (ahí sí cuenta para la limpieza/pago). Si estás
  empujándola, el mismo E vacía sin soltarla.
- Si se **vuelca** (>75°) con carga, **derrama** hasta 6 escombros por el lado que cae (comedia física;
  hay que recogerlos otra vez).
*Tocó:* nuevo `World/Wheelbarrow` (+`WheelbarrowCatcher`), `PlayerMaterials` (transferir/acreditar
escombros), `JobSceneController` (spawn + dumpster devuelve transform).

**Etapa 6 — Madera a medida + cortadora** ✅ *(hecha)*
Cada bloque dañado pide una plancha de medida **mixta**: **2.4 m (lámina entera)**, **1.8 m** o
**1.2 m**. El flujo real *mide → corta → coloca*:
- La estación da **láminas enteras de 2.4 m** (las manos muestran la medida; las planchas sueltas
  conservan su corte y su tamaño visual).
- **Metro sobre el hueco** (un clic en el bloque abierto) = lee la medida que pide ("Hueco: 1.8 m").
- **Sierra sobre el hueco** con la plancha en las manos = la **corta a esa medida** — solo si mediste
  primero (si no: "Mide el hueco con el metro primero"). El **retazo** cae al lado: si es **≥1.2 m**
  queda como **plancha útil** para otro hueco; si es chico, es **escombro** a recoger.
- **Colocar (mano)** solo encaja si la medida coincide (±6 cm); si no, el HUD te dice qué corte falta.
  Los huecos de 2.4 m aceptan la lámina entera sin cortar.
- Las planchas **no se mezclan** en las manos: coger otra suelta la que llevabas (cada una guarda su corte).
*Tocó:* `RoofGrid` (medida por bloque + chequeo al colocar), `ToolFunctions` (metro lee hueco, sierra
corta + retazo), `PlayerMaterials` (longitud de plancha, cortar), `DroppedMaterial` (longitud persistente),
nuevo `HudNotice` (avisos en pantalla), `SupplyStation` (prompt).

**Etapa 7 — Props de la casa e interacción extra** ✅ *(hecha)*
- **Antenas atornilladas:** ✅ (hecho en 2b) quitar tornillos con el taladro → cae → a la basura.
- **Luces de casa:** ✅ luz de **porche** cálida a altura de puerta + 2 luces de **esquina de alero**
  en el lado de entrada (point lights sin sombras, esfera emisiva; costo casi nulo). (`HouseBuilder`)
- **Espumas antideslizantes:** ✅ pila de 3 junto al contenedor. **E** la llevas (flota en tus manos,
  no ocupa el inventario), apuntas al techo y **E** la colocas al ras; pisando **cerca de una espuma
  no resbalas ni pierdes velocidad** por pendiente (`PlayerLocomotion.GripAssist`). *El apoyo de
  rodilla (+calidad) queda a confirmar para más adelante.* (`World/FoamPad`)
- **Apilación / zona de acopio:** ✅ **lona azul** con esquinas marcadas junto al equipamiento;
  soltar (G) encima **encaja el bulto en el hueco más cercano y apila ordenado** sobre lo que ya
  haya (bultos, planchas, rollos) en vez de rodar. (`World/StagingArea`, `DroppedMaterial`)
- **Cuervos:** ✅ 3 cuervos low-poly se posan en la **cumbrera** y el **borde del contenedor**;
  picotean/saltan y giran; al acercarte a <3.4 m **salen volando** con ladeo; vuelven otros al rato.
  (`World/AmbientCrows`)
- *Extra técnico:* los objetos sostenidos (rollo, carretilla, espuma) ahora **reclaman la tecla E**
  (`PlayerInteractor.ClaimInteraction`) — se acabaron los dobles usos de E al soltar mirando otra cosa.

**Etapa 8 — Zoom de precisión al clavar** ✅ *(hecha — REALISMO V2 COMPLETO)*
Con la **clavadora** en mano, **mantén click derecho**: la cámara se **acerca suave** (FOV 60→32),
el **ratón se vuelve fino** (~40% de sensibilidad), la clavadora **se alza al centro** como apuntando,
aparece un **marco de enfoque** que se cierra sobre la mira y el HUD muestra la **precisión EN VIVO**
("Precisión: 87%") de la teja bajo la mira — mueves hasta centrar y disparas. Soltar el botón vuelve
a la vista normal. No es otra escena: solo un acercamiento temporal, integrado con el puntaje de
precisión existente. *Tocó:* `CameraController` (zoom + sensibilidad + anclaje de herramienta),
nuevo `Gameplay/NailZoom` (control + HUD), `RoofGrid` (`PreviewNailQuality`), `RoofingToolbelt`
(pista), `JobSceneController` (cableado).

---

## 8. Fase C — Clima dinámico (roadmap detallado)

> Mismo método que Realismo v2: una etapa por vez, Miguel prueba, ajustamos, seguimos.
> El clima cambia **por fases dentro del nivel** (plan aleatorio: despejado → viento → lluvia →
> tormenta...), con transición suave de ~10 s y **aviso previo** en el HUD para ponerse a salvo.

**Etapa C1 — Núcleo del clima + lluvia + viento** ✅ *(hecha)*
- **`World/WeatherSystem`**: plan de fases aleatorio por nivel (siempre arranca calmado
  —despejado/nublado— para que tomes ritmo; luego dados). Nunca se acaba: va agregando fases.
  HUD arriba a la derecha ("Clima: Lluvia"), y 12 s antes de un cambio real avisa
  "Se acerca: Tormenta (9s)" + HudNotice ("asegúrate: arnés/espumas").
- **El cielo reacciona**: el sol pierde fuerza y se enfría de color, la luz ambiente baja y entra
  una neblina ligera según lo feo que se ponga (la neblina DENSA como fase propia llega en C3).
- **Lluvia**: campo de partículas alargadas que **sigue al jugador** desde arriba (barato, no
  cubre el mapa) y se **inclina con el viento**. El techo se **empapa** con el tiempo (~18 s de
  lluvia = empapado) y entonces **resbala antes y más fuerte**
  (`PlayerLocomotion.SetSlipperiness`: −9° al ángulo de deslizamiento y +75% de tirón cuesta
  abajo a tope de mojado). Al parar, **se seca lento** (~75 s). Las **espumas antideslizantes
  siguen agarrando** — en lluvia valen oro. El HUD marca "techo MOJADO (resbala)".
- **Viento**: empuje continuo vía `PlayerLocomotion.SetWind` que **respira** (ruido Perlin) y
  cuya dirección deriva despacio. **En lo alto estás expuesto** (a nivel de suelo la casa te
  abriga: ×0.3). En Ventoso/Tormenta caen **ráfagas** de empujón extra cada 4–9 s — el arnés (F)
  se gana el sueldo.
- Fases: Despejado, Nublado, Ventoso, Lluvia, Tormenta (lluvia a tope + viento a tope).
*Tocó:* nuevo `World/WeatherSystem`, `PlayerLocomotion` (resbalón por mojado en el ángulo y la
aceleración de deslizamiento), `JobSceneController` (spawn).

**Etapa C2 — Ventisca / nieve** ✅ *(hecha)*
- **Niveles fríos:** ~45% de los niveles son "día frío" — la lluvia sale como **Nevada** y la
  tormenta como **Ventisca** (mismo plan de fases; en día templado no nieva nunca).
- **La nieve se acumula por celda**: cada celda cría una **capa blanca que crece** (a nevada
  fuerte, tapada en ~20-25 s; enterrada en ~50 s). Una celda **tapada no se puede trabajar** con
  ninguna herramienta — HUD: "Celda nevada — límpiala con la pala".
- **La pala limpia**: 1 palada si hay poca, **2 si está enterrada** ("Nieve pesada — otra
  palada"). También se limpian las celdas ya terminadas. Mantener click barre varias celdas.
- **La nieve resbala por sí sola** (85% del efecto mojado) y al **derretirse moja** el techo
  (resbala aunque ya no se vea blanca). Se derrite sola en ~2 min al parar — la pala es más rápida.
- **Ventisca** = nieve + viento a tope + **visibilidad corta de verdad** (niebla blanca densa) +
  ráfagas. Copos con revoloteo (ruido), arrastrados por el viento; luz fría azulada-blanca.
- HUD: "techo NEVADO (pala)" + pista de la pala cuando hay nieve.
*Tocó:* `WeatherSystem` (fases Snow/Blizzard, día frío, campo de copos, deshielo),
`RoofingProcess` (`RoofCell.AddSnow/ShovelSnow/SnowBlocked` + capa visual), `RoofGrid`
(`TickSnow`/`AverageSnow` + bloqueo en el uso de herramientas), `RoofingToolbelt` (pista),
`JobSceneController` (cableado grilla→clima).

**Etapa C3 — Neblina densa + rayo** ✅ *(hecha)*
- **Neblina como fase propia** ("Neblina", ~14% de las fases, también en días fríos): aire
  quieto y **visibilidad corta de verdad** (~10-15 m, niebla exponencial densa, luz lavada).
  Trabajar el techo casi a ciegas — el peligro es no ver el borde.
- **Relámpagos de ambiente** en Tormenta: cada 6-14 s el cielo **destella** (sol + ambiente +
  flash de pantalla). *(El trueno con retardo llega con el audio en Fase G.)*
- **Eres el pararrayos**: en plena tormenta, estar **de pie cerca de la cumbrera** (el punto más
  alto) **carga un rayo sobre ti** (~3.5 s): banner rojo "PELIGRO DE RAYO: BAJA o AGÁCHATE
  (Ctrl)" con **barra de carga** que se llena. **Agacharse o bajar** la descarga rápido.
- **Si te cae**: rayo dentado visible del cielo a ti (LineRenderer, 0.35 s), flash blanco de
  pantalla completa, **empujón que te tira** (aturde) y **−20 de estamina máxima**
  (`PlayerStamina.AddInjury` — sana lento, **sin muerte**). Aviso en HUD.
*Tocó:* `WeatherSystem` (fase Fog + módulo de rayos: carga/aviso/impacto/flash/bolt),
`PlayerLocomotion` (`IsCrouching`), `RoofGrid.CellAt(0,0)` para la altura de cumbrera.

**Etapa C4 — Temperatura (frío/calor)** ✅ *(hecha — FASE C COMPLETA — sigue §9)*
- **Tres tipos de nivel**: día frío (45%, el que nieva), **ola de calor** (~19%) y templado
  (el resto, sin estrés térmico). El HUD muestra el termómetro: "Clima: Nevada · -6°C".
- **FRÍO**: la exposición (nevada, viento que corta, estar en lo alto) acumula **frío corporal**
  → la estamina **regenera hasta 4× más lento**. Te calientas **moviéndote** (trabajar abriga,
  esprintar más) o **bajando** al nivel del suelo (la casa abriga). Aviso azul + tinte azul suave.
- **CALOR**: el **sol directo** acumula **calor corporal** → cada esfuerzo (sprint, salto,
  cargar, colgarte) **cuesta hasta casi el doble**. Te refrescas en la **SOMBRA de verdad**
  (raycast hacia el sol: la casa, el techo o el contenedor te tapan), o cuando nubla/llueve.
  Aviso naranja + tinte cálido suave.
- Ganchos en `PlayerStamina.SetClimate(regenScale, costScale)` — la ropa térmica y el agua/café
  de la **tienda (Fase E)** suavizarán estos factores.
*Tocó:* `WeatherSystem` (módulo de temperatura + termómetro + sombra por raycast),
`PlayerStamina` (escala de regeneración y de costo).

---

## 9. Fase D — Niveles procedurales (roadmap detallado)

> REPO-style: contratos casi aleatorios con **probabilidad creciente de cosas peores** según
> el progreso del career. Todo lo malo paga más.

**Etapa D1 — Generador procedural + tablero de contratos** ✅ *(hecha)*
- **Al darle Play, la escena abre en el TABLERO DE CONTRATOS**: 3 ofertas generadas
  (`Core/LevelGenerator`), de la más mansa **[1]** a la más brava **[3]**. Se toma con **1/2/3**,
  **R** pide otras ofertas, Backspace vuelve. El mundo **no se construye hasta elegir**.
- **Cada oferta muestra**: cliente (nombre con sabor), dificultad 1-5, casa (ancho×fondo,
  **1-3 pisos** — el 3er piso aparece desde el nivel 5), pendiente en ° (suave/empinada/MUY
  empinada, 30-45°), **celdas de techo reales** (la celda conserva su ~1 m de teja: casa más
  grande = más hiladas de verdad = más trabajo), daño de madera (leve/medio/grave, 10-65% de
  bloques), antenas a destornillar (1-2), **pronóstico honesto** (Templado / Frío—nevadas /
  Ola de calor; "tormentoso"/"inestable" según severidad) y **pago estimado**.
- **La dificultad escala con el career** (`totalJobsCompleted`, rampa ~8 trabajos): más grande,
  más empinada, más alta, más podrida, peor clima — con ruido ("casi aleatorio").
- **El clima obedece al contrato**: la severidad **sesga el plan de fases** hacia lo violento
  (sesgo exponencial: los contratos bravos de verdad truenan más) y **acorta la calma inicial**;
  el frío/calor anunciado se cumple (`WeatherSystem.Init`).
- **Todo lo malo paga**: multiplicador de riesgo por tamaño, clima, pisos y frío/calor
  (hasta ~×2). El pago real sigue multiplicando calidad × limpieza.
*Tocó:* nuevo `Core/LevelGenerator` (ofertas), `JobSceneController` (tablero + construcción
diferida + antenas/daño/celdas/pago por contrato), `WeatherSystem` (`Init` severidad+clima,
sesgo de fases), `RoofGrid` (`SetDeckDamageChance`).

**Etapa D2 — Multas por fallo + reintento** ✅ *(hecha)*
- **Abandonar un contrato tomado cuesta multa**: Backspace en obra abre un **overlay de
  confirmación** con el monto — **25% del pago estimado** (mínimo $10). Decisión tomada:
  **deuda permitida** (estilo REPO) — la multa se cobra completa aunque el dinero quede en
  negativo; los próximos pagos la van saldando. El chip del HUD pasa a **"DEUDA: -$X" en rojo**.
- **Opciones del overlay**: **[R]** pagar y **reintentar la misma casa** (el mundo se
  demuele y reconstruye desde cero con el mismo contrato: mismo pronóstico/severidad, plan de
  clima nuevo; `attemptCount` se acumula y queda en el historial del career) · **[Backspace]**
  pagar y **volver al tablero** (ofertas nuevas + aviso de la multa pagada) · **[Enter]**
  cancelar y seguir trabajando. Abandonar **desde el tablero** (sin contrato tomado) sigue
  siendo gratis; tras **entregar**, salir también es gratis.
- **Tras entregar (standalone)**: Backspace vuelve al **tablero** con ofertas re-generadas —
  el bucle REPO completo (trabajo → pago → ofertas más bravas) sin salir del Play. El flujo
  de menú sigue yendo al career overview.
- **Demolición limpia**: `TearDownWorld()` destruye todo lo procedural por diferencia con un
  snapshot de raíces de escena tomado en `Start` (todo lo que construye `BuildWorld` es raíz
  nueva); el reintento espera **1 frame** para que `WeatherSystem.OnDestroy` restaure
  sol/niebla/ambiente antes de que el nuevo capture su línea base.
*Tocó:* `CareerManager.RecordFine` (resta y guarda; deuda permitida), `JobSceneController`
(overlay + multa + teardown/reintento + tablero post-entrega + chip de deuda en rojo;
`CareerMoney()` ahora usa `int.MinValue` como centinela porque -$1 ya es dinero real).

**Pendiente D2+ (ideas)**: multa también por "fallo" no voluntario si algún día existe (p. ej.
tiempo límite); interés sobre la deuda; bloquear contratos de 3 estrellas si debes mucho.

**Etapa D3 — Variedad estructural: obstáculos** ✅ *(hecha)*
Los contratos pueden traer **extras estructurales** (el tablero los anuncia y **pagan más**):
- **Chimenea** (+6% pago; más probable con el nivel): torre de ladrillo cerca de la cumbrera
  que **ocupa una celda real** — la celda sale del juego (no cuenta para completar) con un
  faldón metálico en la base, y el **orden diagonal fluye alrededor** (una celda obstruida
  cuenta como "tejada" para el apoyo escalonado, así las hiladas no mueren contra ella).
- **Claraboya** (+6%): panel de vidrio a ras del techo que ocupa **2 celdas** a media
  pendiente. **Pisarla es un error de novato**: cruje a los ~0.2 s de pararte encima y al
  segundo **CRAC** — lesión (10) + empujón pendiente abajo; el vidrio queda rajado (visible)
  pero pisable, una sola vez por panel.
- **Árbol molesto** (+4%): tronco sólido junto a la subida de la escalera (+X del alero) con
  la copa (sin colisión) invadiendo la trepada — estorba y tapa la esquina del alero.
- Soporte: `RoofGrid.ObstructCell(row,col)` (saca la celda de `cells`, la anula en la malla —
  las operaciones por bloque ya saltan nulls — y la marca en `obstructedMask` para el orden),
  textura procedural de **ladrillo** nueva en `RoofTextureLibrary`.
*Tocó:* nuevo `World/RoofObstacles` (chimenea/claraboya+`SkylightHazard`/árbol), `RoofGrid`
(obstrucción), `LevelGenerator` (3 flags + pago/estrellas), `JobSceneController` (spawn por
contrato + línea "Extras" en la oferta), `RoofTextureLibrary` (Brick).

**Etapa D4 — Geometría nueva: multi-cara + 2 aguas + ala en L** ✅ *(hecha)*
- **`RoofGrid` generalizado a varias caras**: cada cara es su propio grid; todos comparten el
  input del jugador y cada uno responde **solo por sus celdas** (`Owns` — las celdas cuelgan
  del ancla de su cara). Registro estático `live` + `GridOf(celda)` / `GridNear(punto)` para
  que las herramientas por celda (sierra, cuchillo, metro, cap nailer, tiza, zoom de clavadora,
  rollo de fieltro desplegable) **ruteen a la cara dueña**. Cada cara conserva su propio orden
  escalonado, sus hiladas de fieltro y su tiza.
- **Contratos "2 aguas"** (`bothFaces`, cada vez más probables): la cara trasera del gable es
  parte del trabajo — nueva ancla `roofFaceLeft` (con yaw 180° para conservar la convención
  +X = cumbrera→alero). **Se cruza por la cumbrera**. HUD del cinturón agrega todas las caras
  ("Techo: X/Y celdas"); al terminar una cara: aviso "¡Agua terminada! Quedan N celdas…".
- **Ala en L** (`lWing`, requiere 2+ pisos): volumen de 1 piso pegado a la pared −X con
  cumbrera **perpendicular**; su techo queda **bajo el alero principal** (sin valles — como
  las ampliaciones reales). Construida en un marco hijo rotado −90° reutilizando la misma
  matemática de caras verificada. Trae **2 caras propias** (celdas reales ~1 m) y su
  **escalera pequeña**. Un contrato con ala siempre es de techo completo.
- **Clima multi-cara**: `WeatherSystem.RegisterGrid` — la nieve cae/derrite en todas las
  caras; resbalón y HUD usan el promedio ponderado por celdas.
- **Pago/pronóstico**: las celdas extra pagan solas (tarifa por celda); logística suma
  +5% (2 aguas) / +8% (ala); estrellas suben acorde. El tablero anuncia
  "Techo: N celdas (1 agua / 2 aguas / 2 aguas + ALA EN L)".
*Tocó:* `RoofGrid` (registro/dueño/ruteo), `HouseBuilder` (ancla izquierda, `BuildWing`,
`HouseSpec.lWing/wingWidth/wingDepth`, `BuiltHouse.roofFaceLeft/wingFaceA/B/wingFaceSize`),
`JobSceneController` (grids múltiples + agregados de pago/cobertura + aviso por cara),
`RoofingToolbelt` (`SetGrids` + HUD agregado), `ToolFunctions`/`NailZoom` (ruteo),
`WeatherSystem` (`RegisterGrid`, nieve multi-cara), `LevelGenerator` (layout + pago).

**Etapa D5 — Techos a 4 aguas** ✅ *(hecha — cierra la Fase D)*
- **Geometría real de hip roof** (requiere fondo > ancho + 1 m; pendiente igual en las 4
  caras): las caras principales pasan a ser **trapecios**, los hastiales desaparecen y en su
  lugar hay **2 caras triangulares** inclinadas; la cumbrera se acorta a `fondo − ancho`
  (poste de arnés y cuervos acortados acorde). Las caras se construyen como **mallas planas**
  con `MeshCollider` (una caja rectangular sobresaldría atravesando a sus vecinas).
- **Corte de celdas en los limatones**: `RoofGrid.Build` acepta un **predicado** — las celdas
  cuyo centro cae fuera del polígono **nunca se crean** (marcadas obstruidas: el orden
  diagonal fluye alrededor, mismo mecanismo D3; los marcos de bloque de madera vacíos
  tampoco se dibujan). El borde aserrado queda **tapado por tapas de limatón** reales
  (faldón ancho + caperuza levantada, como las hip caps de verdad) y una tapa de cumbrera.
- **El tablero anuncia celdas EXACTAS**: la regla de corte vive en `HipRoofMath`
  (`HouseBuilder.cs`), compartida por el generador (contar) y el constructor (cortar) —
  imposible que diverjan. Etiqueta "(4 AGUAS)" o "(4 AGUAS + ALA EN L)".
- **Un contrato a 4 aguas es siempre de techo completo** (4 caras; compatible con ala en L
  = hasta 6 caras). Pago +8% de logística, estrellas +. El **rayo** ahora escanea la hilada
  de cumbrera buscando una celda viva (la esquina (0,0) no existe en un trapecio); chimenea
  y claraboya se colocan **centradas** para caer dentro del polígono en cualquier techo.
*Tocó:* `HouseBuilder` (`HouseSpec.hipRoof`, rama hip con `RoofMesh`/`BuildHipCaps`, anclas de
punta `endFaceFront/Back`, `ridgeLength`, clase `HipRoofMath`), `RoofGrid` (predicado
`keepCell` en `Build`, bordes de bloque huérfanos), `JobSceneController` (4-6 grids con
predicados, cuervos/obstáculos), `WeatherSystem` (escaneo de cumbrera), `LevelGenerator`
(flag + `TotalCells` exacto + pago).

**Fase D — COMPLETA** (D1 contratos, D2 multas/deuda, D3 obstáculos, D4 multi-cara/ala en L,
D5 cuatro aguas). Siguiente fase natural: **E — Economía y tienda** (§5, desglose en §10).

---

## 10. Fase E — Economía y tienda (roadmap detallado)

> El dinero del career por fin tiene destino. Regla de oro: todo upgrade **ayuda sin
> automatizar** — suaviza un peligro que el jugador sigue teniendo que respetar. Y regla
> de caja: **la tienda no fía** (solo dinero en mano); las multas de la D2 sí endeudan.

**Etapa E1 — Tienda + equipo permanente** ✅ *(hecha)*
- **[T] en el tablero de contratos** abre la **TIENDA DEL TECHADOR**: lista de equipo con
  precio y descripción, se compra con **[1-5]**, [T]/[Backspace] vuelve. Lo comprado queda
  marcado "EN TU EQUIPO" y **persiste en el save** (`Career.ownedUpgrades`, aditivo — los
  saves viejos cargan con lista vacía).
- **Catálogo E1** (`Core/Shop.cs`, efectos cableados por `JobSceneController.ApplyOwnedGear`
  al construir cada mundo — los sistemas solo reciben floats, nunca leen el career):
  - **Cantimplora $250** — el calor acumula ×0.5 en olas de calor (`WeatherSystem.SetProtection`).
  - **Ropa térmica $300** — el frío cala ×0.5 (misma vía; recorta la exposición, no el calentarse).
  - **Botas de agarre $350** — resbalón −45% (`PlayerLocomotion.SetSlipResistance`; las
    espumas siguen siendo el agarre total).
  - **Arnés reforzado $400** — lesión por caída −40% (`LedgeGrabAndFall.SetFallProtection`;
    el stun por caídas grandes se mantiene).
  - **Clavadora calibrada $500** — centro más generoso al clavar (`RoofGrid.SetPrecisionAid`:
    borde 0.4→0.61 de calidad; clavar centrado sigue siendo lo óptimo).
- **Compra**: `CareerManager.TryBuyUpgrade(id, price)` — rechaza sin fondos (sin deuda),
  guarda al instante; `HasUpgrade(id)` para consultar. Avisos en el panel ("no te alcanza…").
*Tocó:* nuevo `Core/Shop.cs`, `Career.ownedUpgrades`, `CareerManager` (HasUpgrade/TryBuy),
`JobSceneController` (tienda UI + ApplyOwnedGear), setters nuevos en `PlayerLocomotion`,
`LedgeGrabAndFall`, `WeatherSystem`, `RoofGrid`.

**Etapa E2 — Consumibles** ✅ *(hecha)*
- **Se compran por packs en la tienda** (teclas **[6-8]**, stackean, persisten hasta gastarse:
  `Career.consumableCharges`, una entrada por carga): **Sal de deshielo ×3 — $120**,
  **Termo de café ×2 — $90**, **Cuerda de emergencia ×1 — $150**.
- **En la obra** (nuevo `Gameplay/ConsumableBelt`, chip abajo a la izquierda): **[X]** cambia
  el consumible seleccionado (solo muestra lo que llevas), **[Q]** lo usa:
  - **Sal**: derrite la nieve de todas las celdas a ~2.4 m del punto que miras (más rápido
    que palear; si no hay nieve **no gasta la carga**).
  - **Café**: estamina al instante (+35), cura un poco la lesión (−8) y **regenera ×1.5
    durante 75 s** (`PlayerStamina.DrinkCoffee`; se apila con cortesía).
  - **Cuerda de emergencia**: se **arma** con Q y **absorbe por completo la próxima caída
    dura** — lesión y aturdimiento (`LedgeGrabAndFall.ArmEmergencyRope`; no se puede armar
    doble; el chip muestra "(ARMADA)").
- Misma arquitectura E1: el cinturón **no lee el career** — recibe callbacks de contar/gastar
  (`CareerManager.ConsumableCount/TryBuyConsumable/TryUseConsumable`, guardado al instante).
*Tocó:* nuevo `Gameplay/ConsumableBelt.cs`, `Shop.Packs` (+`ShopItem.charges`),
`Career.consumableCharges`, `CareerManager` (×3 métodos), `PlayerStamina.DrinkCoffee`,
`LedgeGrabAndFall` (cuerda), `JobSceneController` (spawn + compra + sección en la tienda).

**Etapa E3 — Costo de materiales (versión suave)** ✅ *(hecha — cierra la Fase E)*
Decisión de Miguel: **cobrar solo el desperdicio**, no cada bundle.
- **Cada contrato incluye un presupuesto de material** dimensionado al techo REAL recién
  construido (+margen de trabajo): tejas = celdas ×1.25, fieltro = celdas ×1.35, planchas =
  bloques dañados vivos +34% (nuevo `RoofGrid.DamagedBlocks` — los bloques vaciados por el
  corte de 4 aguas no cuentan). Se anuncia al arrancar la obra ("Material incluido: …").
- **Recargar dentro del presupuesto es gratis**; pasarse descuenta del pago final: teja
  extra $4 · fieltro extra $3 · plancha extra $12. **Recoger material del suelo y reutilizar
  retazos es gratis** — solo cuenta lo tomado del pallet (`PlayerMaterials.Restocked`, que
  cuenta el top-up real de `TryLoad`).
- **Aviso en vivo**: chip rojo "Material de más: -$X" sobre el dinero en cuanto revientas el
  presupuesto; la pantalla de entrega desglosa "Material extra: -$X". `JobCompletion.materialUsed`
  ahora registra las unidades reales recargadas.
*Tocó:* `PlayerMaterials` (contadores de recarga), `RoofGrid.DamagedBlocks`,
`JobSceneController` (presupuesto al construir + descuento en el pago + avisos).

**Fase E — COMPLETA** (E1 tienda/equipo, E2 consumibles, E3 presupuesto de material).
Siguientes fases: **F — Multijugador** (sincronizar el flujo nuevo con Mirror) y
**G — Arte y audio** (§5).
