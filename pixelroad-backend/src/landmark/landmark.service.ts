import { Injectable } from '@nestjs/common';
import { readFile } from 'fs/promises';
import { join } from 'path';
import { Landmark } from './landmark.interface';

// 랜드마크 데이터 관리
@Injectable()
export class LandmarkService {
    // 랜드마크 제이슨 읽기
    private readonly filePath = join(process.cwd(),
        'data', 'landmarks.json');

    // 모든 랜드마크 읽기
    async getAllLandmarks() {
        const data = await readFile(this.filePath, 'utf-8');
        return JSON.parse(data) as Landmark[];
    }

    // 랜드마크 조회
    async getLandmarkById(id: number): Promise<Landmark | undefined> {
        const landmarks = await this.getAllLandmarks();

        return landmarks.find((landmark) => landmark.id === id);
    }
}
