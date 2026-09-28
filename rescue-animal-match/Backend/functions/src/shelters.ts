/**
 * getShelters (HTTPS): returns verified shelters with their adoptable animals.
 * Public read endpoint for the AdoptionScreen and web landing pages.
 */
import { onRequest } from 'firebase-functions/v2/https';
import * as admin from 'firebase-admin';
import type { ShelterDoc } from './types';

export interface PublicShelter {
  id: string;
  name: string;
  location: string;
  contact: string;
  needs: string[];
  animals: ShelterDoc['animals'];
}

function toPublic(id: string, doc: ShelterDoc): PublicShelter {
  return {
    id,
    name: doc.name ?? '',
    location: doc.location ?? '',
    // Contact is only exposed for VERIFIED shelters (checked by query below).
    contact: doc.contact ?? '',
    needs: Array.isArray(doc.needs) ? doc.needs : [],
    animals: Array.isArray(doc.animals) ? doc.animals : [],
  };
}

export const getShelters = onRequest(async (_req, res) => {
  try {
    const snap = await admin
      .firestore()
      .collection('shelters')
      .where('verified', '==', true)
      .get();

    const shelters: PublicShelter[] = snap.docs.map((d) =>
      toPublic(d.id, d.data() as ShelterDoc),
    );

    res.set('Cache-Control', 'public, max-age=60');
    res.status(200).json({ count: shelters.length, shelters });
  } catch (err) {
    res.status(500).json({ error: 'shelters_lookup_failed' });
  }
});
