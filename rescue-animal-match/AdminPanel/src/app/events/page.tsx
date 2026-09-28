"use client";

import { useCallback, useEffect, useState } from "react";
import type { Shelter, WeeklyGoal } from "@/lib/types";

/**
 * /events — crea el objetivo semanal: meta de Huellas, refugio beneficiado y
 * descripción del impacto. Escribe config/weekly_goal vía /api/events.
 */
export default function EventsPage() {
  const [goal, setGoal] = useState<WeeklyGoal | null>(null);
  const [shelters, setShelters] = useState<Shelter[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState({
    targetHuellas: "10000",
    shelterId: "",
    rewardDescription: "",
  });

  const token = () => sessionStorage.getItem("ram_admin_token") ?? "";
  const headers = { Authorization: `Bearer ${token()}` };

  const load = useCallback(async () => {
    const [g, s] = await Promise.all([
      fetch("/api/events", { headers }).then((r) => r.json()),
      fetch("/api/shelters", { headers }).then((r) => r.json()),
    ]);
    if (g?.error) setError(g.error);
    else setGoal(g as WeeklyGoal);
    if (Array.isArray(s)) {
      setShelters(s.filter((x: Shelter) => x.verified));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function createEvent(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    const shelter = shelters.find((s) => s.id === form.shelterId);
    if (!shelter) {
      setError("selecciona_refugio_valido");
      return;
    }
    const res = await fetch("/api/events", {
      method: "POST",
      headers: { ...headers, "Content-Type": "application/json" },
      body: JSON.stringify({
        targetHuellas: Number(form.targetHuellas),
        shelterId: shelter.id,
        shelterName: shelter.name,
        rewardDescription: form.rewardDescription,
      }),
    });
    if (!res.ok) {
      setError((await res.json()).error ?? "create_failed");
      return;
    }
    setGoal(await res.json());
    setForm({ ...form, rewardDescription: "" });
  }

  return (
    <div>
      <h1>Eventos semanales</h1>
      {error && <p className="error">{error}</p>}

      {goal && (
        <div className="card">
          <h2 style={{ marginTop: 0 }}>Evento activo</h2>
          <p>
            <strong>{goal.shelterName}</strong> — meta{" "}
            {goal.targetHuellas.toLocaleString("es-MX")} Huellas (progreso actual{" "}
            {goal.currentHuellas.toLocaleString("es-MX")})
          </p>
          <p className="muted">{goal.rewardDescription}</p>
        </div>
      )}

      <h2>Crear nuevo evento</h2>
      <form className="auth" onSubmit={createEvent}>
        <input
          type="number"
          min={1000}
          placeholder="Meta de Huellas (≥1000)"
          value={form.targetHuellas}
          onChange={(e) => setForm({ ...form, targetHuellas: e.target.value })}
          required
        />
        <select
          value={form.shelterId}
          onChange={(e) => setForm({ ...form, shelterId: e.target.value })}
          required
        >
          <option value="">Selecciona refugio verificado…</option>
          {shelters.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name} — {s.location}
            </option>
          ))}
        </select>
        <textarea
          rows={3}
          placeholder="Descripción del impacto (mín. 10 caracteres)"
          value={form.rewardDescription}
          onChange={(e) =>
            setForm({ ...form, rewardDescription: e.target.value })
          }
          required
        />
        <button type="submit">Publicar evento semanal</button>
      </form>
    </div>
  );
}
