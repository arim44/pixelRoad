import { Module } from '@nestjs/common';
import { LandmarkService } from './landmark.service';

@Module({
  providers: [LandmarkService]
})
export class LandmarkModule {}
