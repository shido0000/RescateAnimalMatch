import { listLevelMeta, listPushes } from "@/lib/adminData";

export const dynamic = "force-dynamic";

/**
 * /content — metadatos de niveles (los 30 del juego), cosméticos disponibles
 * y borradores de notificaciones push.
 */
export default async function ContentPage() {
  const levels = await listLevelMeta();
  const pushes = await listPushes();

  return (
    <div>
      <h1>Contenido del juego</h1>

      <h2>Niveles ({levels.length})</h2>
      <table>
        <thead>
          <tr>
            <th>#</th>
            <th>Nombre</th>
            <th>Movimientos</th>
            <th>Objetivos</th>
            <th>Dificultad</th>
          </tr>
        </thead>
        <tbody>
          {levels.map((l) => (
            <tr key={l.levelNumber}>
              <td>{l.levelNumber}</td>
              <td>{l.levelName}</td>
              <td>{l.movesLimit}</td>
              <td>{l.objectiveCount}</td>
              <td>
                <span className={`badge ${l.difficulty === "hard" ? "pending" : "ok"}`}>
                  {l.difficulty}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <h2 style={{ marginTop: 32 }}>Notificaciones push</h2>
      <table>
        <thead>
          <tr>
            <th>Título</th>
            <th>Cuerpo</th>
            <th>Estado</th>
          </tr>
        </thead>
        <tbody>
          {pushes.map((p) => (
            <tr key={p.id}>
              <td>{p.title}</td>
              <td>{p.body}</td>
              <td>
                <span className={`badge ${p.sent ? "ok" : "pending"}`}>
                  {p.sent ? "Enviada" : "Borrador"}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <h2 style={{ marginTop: 32 }}>Cosméticos</h2>
      <p className="muted">
        Temas de tablero, fondos y mascotas virtuales se gestionan como
        ScriptableObjects en Assets/Resources/Cosmetics/ y se sincronizan con
        Firestore (users/{'{uid}'}.equipped).
      </p>
    </div>
  );
}
