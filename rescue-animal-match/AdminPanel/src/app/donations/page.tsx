"use client";

import { useCallback, useEffect, useState } from "react";
import type { Donation } from "@/lib/types";

/**
 * /donations — lista de donaciones creadas por la Cloud Function
 * onDonationReached. Permite marcar como verificada y subir comprobante.
 */
export default function DonationsPage() {
  const [donations, setDonations] = useState<Donation[]>([]);
  const [error, setError] = useState<string | null>(null);

  const token = () => sessionStorage.getItem("ram_admin_token") ?? "";

  const load = useCallback(async () => {
    const res = await fetch("/api/donations", {
      headers: { Authorization: `Bearer ${token()}` },
    });
    if (!res.ok) {
      setError((await res.json()).error ?? "load_failed");
      return;
    }
    setDonations(await res.json());
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function verify(d: Donation) {
    setError(null);
    const proofUrl = prompt(
      "URL del comprobante (recibo del refugio):",
      d.proofUrl ?? "",
    );
    const res = await fetch("/api/donations/verify", {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token()}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ donationId: d.id, proofUrl: proofUrl || undefined }),
    });
    if (!res.ok) {
      setError((await res.json()).error ?? "verify_failed");
      return;
    }
    const updated: Donation = await res.json();
    setDonations((list) => list.map((x) => (x.id === updated.id ? updated : x)));
  }

  return (
    <div>
      <h1>Donaciones</h1>
      {error && <p className="error">{error}</p>}
      <table>
        <thead>
          <tr>
            <th>Fecha</th>
            <th>Refugio</th>
            <th>Monto USD</th>
            <th>Huellas</th>
            <th>Tipo</th>
            <th>Estado</th>
            <th>Comprobante</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {donations.map((d) => (
            <tr key={d.id}>
              <td>{d.date}</td>
              <td>{d.shelterName ?? d.shelterId}</td>
              <td>${d.amountUsd.toLocaleString("es-MX")}</td>
              <td>{d.huellas.toLocaleString("es-MX")}</td>
              <td>{d.type}</td>
              <td>
                <span className={`badge ${d.verified ? "ok" : "pending"}`}>
                  {d.verified ? "Verificada" : "Pendiente"}
                </span>
              </td>
              <td>{d.proofUrl ? "📄 adjunto" : "—"}</td>
              <td>
                {!d.verified && (
                  <button onClick={() => void verify(d)}>Verificar</button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
