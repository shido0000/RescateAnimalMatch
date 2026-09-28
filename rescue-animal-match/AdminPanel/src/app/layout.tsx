import type { Metadata } from "next";
import "./globals.css";
import { NavLinks } from "@/components/NavLinks";

export const metadata: Metadata = {
  title: "Rescate Animal Match — Admin",
  description: "Panel de administración: refugios, donaciones, eventos y métricas",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="es">
      <body>
        <div className="shell">
          <aside className="sidebar">
            <div className="brand">🐾 RAM Admin</div>
            <NavLinks />
          </aside>
          <main className="content">{children}</main>
        </div>
      </body>
    </html>
  );
}
