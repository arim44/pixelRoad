import { Module } from '@nestjs/common';
import { LandmarkService } from './landmark.service';

@Module({
  providers: [LandmarkService],
  exports:[LandmarkService],
})
export class LandmarkModule {}
