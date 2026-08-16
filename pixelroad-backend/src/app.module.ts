import { Module } from '@nestjs/common';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { AiModule } from './ai/ai.module';
import { LandmarkModule } from './landmark/landmark.module';

@Module({
  imports: [AiModule, LandmarkModule],
  controllers: [AppController],
  providers: [AppService],
})
export class AppModule {}
