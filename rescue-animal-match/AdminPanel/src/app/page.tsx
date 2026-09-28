import Link from "next/link";

export default function Home() {
  return (
    <div>
      <h1>Rescate Animal Match — Panel de Administración</h1>
      <p className="muted">
        Gestiona refugios verificados, donaciones transparentes, eventos
        semanales de Huellas y contenido del juego.
      </p>
      <ul>
        <li>
          <Link href="/login">Iniciar sesión (solo administradores)</Link>
        </li>
        <li>
          <Link href="/dashboard">Dashboard de impacto</Link>
        </li>
      </ul>
    </div>
  );
}
