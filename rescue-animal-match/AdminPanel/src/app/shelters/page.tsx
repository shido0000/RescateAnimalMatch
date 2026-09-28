"use client";

import { useCallback, useEffect, useState } from "react";
import type { Shelter } from "@/lib/types";

/**
 * /shelters — CRUD de refugios: crear/editar, verificar/desverificar y
 * gestionar animales en adopción. Habla con /api/shelters.
 */
export default function SheltersPage() {
  const [shelters, setShelters] = useState<Shelter[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [draft, setDraft] = useState<Shelter>({
    id: "",
    name: "",
    location: "",
    contact: "",
    needs: [],
    animals: [],
    verified: false,
  });

  const token = () => sessionStorage.getItem("ram_admin_token") ?? "";

  const load = useCallback(async () => {
    const res = await fetch("/api/shelters", {
      headers: { Authorization: `Bearer ${token()}` },
    });
    if (!res.ok) {
      setError((await res.json()).error ?? "load_failed");
      return;
    }
    setShelters(await res.json());
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function save(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    const res = await fetch("/api/shelters", {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token()}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(draft),
    });
    if (!res.ok) {
      setError((await res.json()).error ?? "save_failed");
      return;
    }
    await load();
    setDraft({ ...draft, id: "", name: "", location: "", contact: "" });
  }

  async function toggleVerified(s: Shelter) {
    await fetch("/api/shelters", {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token()}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ ...s, verified: !s.verified }),
    });
    await load();
  }

  function addAnimal() {
    const name = prompt("Nombre del animal:");
    if (!name) return;
    const species = prompt("Especie (perro/gato/otros):") ?? "otros";
    const ageMonths = Number(prompt("Edad en meses:") ?? "12");
    setDraft((d) => ({
      ...d,
      animals: [
        ...d.animals,
        {
          id: `a_${Date.now()}`,
          name,
          species,
          ageMonths: Number.isFinite(ageMonths) ? ageMonths : 12,
        },
      ],
    }));
  }

  return (
    <div>
      <h1>Refugios</h1>
      {error && <p className="error">{error}</p>}

      <table>
        <thead>
          <tr>
            <th>Nombre</th>
            <th>Ubicación</th>
            <th>Contacto</th>
            <th>Animales</th>
            <th>Estado</th>
            <th>Acciones</th>
          </tr>
        </thead>
        <tbody>
          {shelters.map((s) => (
            <tr key={s.id}>
              <td>{s.name}</td>
              <td>{s.location}</td>
              <td>{s.contact}</td>
              <td>{s.animals.length}</td>
              <td>
                <span className={`badge ${s.verified ? "ok" : "pending"}`}>
                  {s.verified ? "Verificado" : "Pendiente"}
                </span>
              </td>
              <td style={{ display: "flex", gap: 8 }}>
                <button className="secondary" onClick={() => void toggleVerified(s)}>
                  {s.verified ? "Desverificar" : "Verificar"}
                </button>
                <button className="secondary" onClick={() => setDraft(s)}>
                  Editar
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <h2 style={{ marginTop: 24 }}>Crear / editar refugio</h2>
      <form className="auth" onSubmit={save}>
        <input
          placeholder="ID (vacío = nuevo)"
          value={draft.id}
          onChange={(e) => setDraft({ ...draft, id: e.target.value })}
        />
        <input
          placeholder="Nombre"
          value={draft.name}
          onChange={(e) => setDraft({ ...draft, name: e.target.value })}
          required
        />
        <input
          placeholder="Ubicación"
          value={draft.location}
          onChange={(e) => setDraft({ ...draft, location: e.target.value })}
          required
        />
        <input
          placeholder="Contacto (email o WhatsApp)"
          value={draft.contact}
          onChange={(e) => setDraft({ ...draft, contact: e.target.value })}
          required
        />
        <input
          placeholder="Necesidades separadas por coma"
          value={draft.needs.join(", ")}
          onChange={(e) =>
            setDraft({
              ...draft,
              needs: e.target.value.split(",").map((x) => x.trim()).filter(Boolean),
            })
          }
        />
        <button type="button" className="secondary" onClick={addAnimal}>
          + Agregar animal ({draft.animals.length})
        </button>
        <label>
          <input
            type="checkbox"
            checked={draft.verified}
            onChange={(e) => setDraft({ ...draft, verified: e.target.checked })}
          />{" "}
          Verificado (NGO registrada)
        </label>
        <button type="submit">Guardar</button>
      </form>
    </div>
  );
}
